#pragma warning disable CA2208 // Instantiate argument exceptions correctly
#pragma warning disable CA1307 // Specify StringComparison
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
using NuKeeper.Abstractions.Configuration;
using NuKeeper.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuKeeper.ConfigurationProviders
{
    internal class ProvideContext : IProvideConfiguration
    {
        private readonly IFileSettingsCache _fileSettings;
        private readonly CommandBase _command;

        // there should have been application commands, not only CLI commands
        public ProvideContext(IFileSettingsCache fileSettings, CommandBase command)
        {
            _fileSettings = fileSettings;
            _command = command;
        }

        // set up chain of responsibility
        public async Task<ValidationResult> ProvideAsync(SettingsContainer settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            try
            {
                var commitTemplateContext = _command.Context?.ToDictionary(
                    kvp => kvp.Substring(0, kvp.IndexOf('=', 0)),
                    kvp => (object)kvp.Substring(kvp.IndexOf('=', 0) + 1)
                ) ?? _fileSettings.GetSettings().Context;

                foreach (var property in commitTemplateContext?.Keys ?? Enumerable.Empty<string>())
                {
                    settings.UserSettings.Context.Add(property, commitTemplateContext[property]);
                }

                return await ParseDelegatesAsync(settings.UserSettings.Context);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                return ValidationResult.Failure(ex.Message);
            }
        }

        //todo: support complex properties: array, object, delegates recursively.
        // what kind of syntax would be good for this?
        // I don't want to have to guess which properties are delegates as they require evaluation by csharp engine
        // complex property can be constructed using `:`, which would create or access a property on one level below the current one.
        // for example `color=green` would create an object `{ "color": "green" }`, while `person:color=green` would create an object `{ "person": { "color": "green" } }`
        // specifying the same property multiple times would turn it into an array, for example:
        // `person:colors=green` and `person:colors=purple` would result in `{ "person": { "colors": ["green", "purple"] } }`
        // the same could be achieved using commas but only for simple properties
        // `person:colors=green,purple`
        // complex properties would require some other syntax, maybe braces like this?
        // `person:colors=(R:50 G:25 B:75),(R:25 G:25 B:50)`
        // anyway, I don't think this is very important. Complex things should be specified in the configuration file for now.
        //private static IEnumerable<KeyValuePair<string, object>> ParseCommandContext(string[] keyValuePairs)
        //{
        //    foreach (var keyValuePair in keyValuePairs ?? Enumerable.Empty<string>())
        //    {
        //        var kvp = keyValuePair.ToUpperInvariant();

        //        if (!kvp.Contains('='))
        //        {
        //            //todo customized exception
        //            throw new ArgumentException("Missing '=' in key value pair of commit context argument.", nameof(CommandBase.Context));
        //        }

        //        yield return ParseSimpleProperty(keyValuePair);
        //    }
        //}

        //private static KeyValuePair<string, object> ParseSimpleProperty(string simplePropertyString)
        //{
        //    var key = simplePropertyString.Substring(0, simplePropertyString.IndexOf('='));
        //    var value = simplePropertyString.Substring(key.Length + 1);

        //    return new KeyValuePair<string, object>(
        //        key, value
        //    );
        //}

        private static async Task<ValidationResult> ParseDelegatesAsync(IDictionary<string, object> context)
        {
            if (context == null) return ValidationResult.Success;

            const string delegateKey = "_delegates";

            if (context.ContainsKey(delegateKey))
            {
                var delegates = context[delegateKey] as IDictionary<string, string>
                    ?? JsonConvert.DeserializeObject<IDictionary<string, string>>(
                        context[delegateKey].ToString()
                    );

                foreach (var property in delegates.Keys)
                {
                    try
                    {
                        var @delegate = await ParseDelegateAsync(delegates[property]);
                        context.Add(property, @delegate);
                    }
                    catch (CompilationErrorException ex)
                    {
                        return ValidationResult.Failure(ex.Message);
                    }
                }

                context.Remove(delegateKey);
            }

            return ValidationResult.Success;
        }

        private static Task<object> ParseDelegateAsync(string delegateString)
        {
            return CSharpScript.EvaluateAsync(delegateString);
        }
    }
}
