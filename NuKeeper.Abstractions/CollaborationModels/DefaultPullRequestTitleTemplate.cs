using NuKeeper.Abstractions.CollaborationPlatform;

namespace NuKeeper.Abstractions.CollaborationModels
{
    // todo: class is almost identical to CommitWorder. Find the hidden abstraction
    // This class needs to:
    // 1. add things to the context based on the pullrequest, target project,...
    // 2. ~~define a default template~~, and then forward the actual rendering to something else
    // The template actually belongs to the settings or to an implementation of ITemplateRenderer, since a different kind of
    // template renderer will need a different template

    public class DefaultPullRequestTitleTemplate : UpdateMessageTemplate
    {
        public DefaultPullRequestTitleTemplate()
            : base(new StubbleTemplateRenderer()) { }

        //TODO
        public static string DefaultTemplate { get; } =
            "Automatic update of {{^multipleChanges}}{{#packages}}{{Name}} to {{Version}}{{/packages}}{{/multipleChanges}}{{#multipleChanges}}{{packageCount}} packages{{/multipleChanges}}";

        public string CustomTemplate { get; set; }

        public override string Value => CustomTemplate ?? DefaultTemplate;

    }
}
