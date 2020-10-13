using NuKeeper.Abstractions.CollaborationPlatform;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuKeeper.Abstractions.CollaborationModels
{
    // A template defines a contract about the properties (placeholders) it uses
    // A template uses a particular syntax, so it's tied to a particular templating engine

    // We will have different templates, however they will work with the same base properties. They can of course define additional ones.
    // The template should have a method that returns the result of applying itself to its current context. This aligns with the premise of it being bound to a particular engine.


    // visitor? Some sort of "Apply(IEnrichContext...)" method?
    // delegate?


    // packageupdateset is for one package, one version
    // if you want to upgrade a package to 2 different versions, one version for one group of projects, you will have 2 packageupdatesets
    // option 1: single "packages" property

    // multipleChanges indicates multiple packageupdatesets

    //{
    //  "packageEmoji": "📦",
    //  "multipleChanges": true,
    //  "packageCount": 3,
    //  "packages": [
    //    {
    //      "name": "foo.bar",
    //      "version": "1.2.3",
    //      "allowedChange": "Minor",
    //      "actualChange': "Patch",
    //      "publication": {
    //        "date": "2019-06-02",
    //        "ago": "6 months ago"
    //      },
    //      "projectsUpdated": 1,
    //      "latestVersion": {
    //        "version": "2.1.0",
    //        "url": "https://nuget.com/foo.bar/2.1.0",
    //        "publication": {
    //          "date": "2020-01-09",
    //          "ago": "5 weeks ago"
    //        }
    //      },
    //      "updates": [
    //        {
    //          "sourceFilePath": "project_x/packages.config",
    //          "fromVersion": "1.2.1",
    //          "fromUrl": "https://nuget.com/foo.bar/1.2.1",
    //          "toVersion": "1.2.3",
    //          "last": true
    //        }
    //      ],
    //      "sourceUrl": "https://nuget.com/",
    //      "url": "https://nuget.com/foo.bar/1.2.3",
    //      "isFromNuget": true,
    //      "fromVersion": "1.2.1",
    //      "multipleProjectsUpdated": false,
    //      "multipleUpdates": false,
    //      "last": false
    //    },
    //    {
    //      "name": "notfoo.bar",
    //      "version": "1.9.3",
    //      "allowedChange": "Major",
    //      "actualChange": "Minor",
    //      "publication": {
    //        "date": "2019-08-02",
    //        "ago": "5 1/2 months ago"
    //      },
    //      "projectsUpdated": 2,
    //      "latestVersion": {
    //        "version": "1.9.3",
    //        "url": "https://nuget.com/notfoo.bar/1.9.3",
    //        "publication": {
    //          "date": "2020-02-01",
    //          "ago": "2 weeks ago"
    //        }
    //      },
    //      "updates": [
    //        {
    //          "sourceFilePath": "project_x/packages.config",
    //          "fromVersion": "1.1.0",
    //          "fromUrl": "https://nuget.com/notfoo.bar/1.1.0",
    //          "toVersion": "1.9.3",
    //          "last": false
    //        },
    //        {
    //          "sourceFilePath": "project_y/packages.config",
    //          "fromVersion": "1.2.9",
    //          "fromUrl": "https://nuget.com/notfoo.bar/1.2.9",
    //          "toVersion": "1.9.3",
    //          "last": true
    //        }
    //      ],
    //      "sourceUrl": "https://nuget.com",
    //      "url": "https://nuget.com/notfoo.bar/1.9.3",
    //      "isFromNuget": true,
    //      "fromVersion": "",
    //      "multipleProjectsUpdated": true,
    //      "multipleUpdates": true
    //      "last": true
    //    }
    //  ],
    //  "projectsUpdated": 2,
    //  "multipleProjects": true,
    //  "footer": {
    //    "nukeeperUrl": "https://github.com/NuKeeper/NuKeeper",
    //    "warningMsg": "**NuKeeper**: https://github.com/NuKeeperDotNet/NuKeeper"
    //  }
    //}

    public abstract class UpdateMessageTemplate
    {
        //todo: protected??
        private IDictionary<string, object> _persistedContext { get; } = new Dictionary<string, object>();

        protected ITemplateRenderer Renderer { get; }

        protected UpdateMessageTemplate(ITemplateRenderer renderer)
        {
            Renderer = renderer;
            InitializeContext();
        }

        /// <summary>
        ///     Container for all placeholder replacement values, which will be passed to the <see cref="ITemplateRenderer.Render(string, object)"/> as the view.
        /// </summary>
        protected IDictionary<string, object> Context { get; } = new Dictionary<string, object>();

        /// <summary>
        ///     The template proper containing placeholders.
        /// </summary>
        public abstract string Value { get; }

        // todo: add additional properties
        // todo: strongly typed...?
        public IList<PackageTemplate> Packages
        {
            get
            {
                Context.TryGetValue(Constants.Template.Packages, out var packages);
                return packages as IList<PackageTemplate>;
            }
        }

        public FooterTemplate Footer
        {
            get
            {
                Context.TryGetValue(Constants.Template.Footer, out var footer);
                return footer as FooterTemplate;
            }
            set
            {
                Context[Constants.Template.Footer] = value;
            }
        }

        public bool MultipleChanges => Packages?.Count > 1;
        public int PackageCount => Packages?.Count ?? 0;
        public int ProjectsUpdated => Packages?
            .SelectMany(p => p.Updates)
            .Select(u => u.SourceFilePath)
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .Count() ?? 0;

        public bool MultipleProjects => ProjectsUpdated > 1;

        /// <summary>
        ///     Clear all current values for placeholders in the template.
        /// </summary>
        // virtual? or templated method
        public virtual void Clear()
        {
            Context.Clear();
            InitializeContext();
        }

        /// <summary>
        ///     Add a new value for a placeholder to the template. This is only useful in case you define your own custom template.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="persist">Persist the value after <see cref="Clear"/></param>
        // virtual? or templated method
        public void AddPlaceholderValue<T>(string key, T value, bool persist = false)
        {
            // prevent override of real properties?
            Context[key] = value;
            if (persist) _persistedContext[key] = value;
        }

        /// <summary>
        ///     Get the value from a placeholder in the template.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T GetPlaceholderValue<T>(string key)
        {
            if (Context.TryGetValue(key, out var value))
                return (T)value;
            else
                return default;
        }

        /// <summary>
        ///     Output the template with all its placeholders replaced by the current values.
        /// </summary>
        /// <returns></returns>
        // virtual? or templated method
        public virtual string Output()
        {
            var packages = Packages
                .Select(
                    (p, i) => new
                    {
                        p.ActualChange,
                        p.AllowedChange,
                        p.LatestVersion,
                        p.Name,
                        p.ProjectsUpdated,
                        p.Publication,
                        Updates = p.Updates.Select(
                            (u, j) => new
                            {
                                u.SourceFilePath,
                                u.FromVersion,
                                u.FromUrl,
                                u.ToVersion,
                                Last = j == p.Updates.Length
                            }
                        ).ToArray(),
                        p.SourceUrl,
                        p.Url,
                        p.IsFromNuget,
                        p.Version,
                        p.FromVersion,
                        p.MultipleProjectsUpdated,
                        p.MultipleUpdates,
                        Last = i == Packages.Count
                    }
                )
                .ToArray();

            var context = new Dictionary<string, object>(Context)
            {
                [Constants.Template.MultipleChanges] = MultipleChanges,
                [Constants.Template.PackageCount] = PackageCount,
                [Constants.Template.Packages] = packages,
                [Constants.Template.ProjectsUpdated] = ProjectsUpdated,
                [Constants.Template.MultipleProjects] = MultipleProjects
            };

            return Renderer.Render(Value, context);
        }

        private void InitializeContext()
        {
            Context[Constants.Template.Packages] = new List<PackageTemplate>();
            foreach (var kvp in _persistedContext)
            {
                Context[kvp.Key] = kvp.Value;
            }
        }
    }
}
