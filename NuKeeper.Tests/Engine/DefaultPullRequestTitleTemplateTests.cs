using NUnit.Framework;
using NuKeeper.Abstractions.CollaborationModels;
using NuKeeper.Abstractions.RepositoryInspection;
using NuKeeper.Abstractions.CollaborationPlatform;

namespace NuKeeper.Tests.Engine
{
    public class DefaultPullRequestTitleTemplateTests
    {
        private DefaultPullRequestTitleTemplate _sut;
        private IEnrichContext<PackageUpdateSet, UpdateMessageTemplate> _enricher;
        private PackageUpdateSetsEnricher _multiEnricher;

        [SetUp]
        public void TestInitialize()
        {
            _sut = new DefaultPullRequestTitleTemplate();
            _enricher = new PackageUpdateSetEnricher();
            _multiEnricher = new PackageUpdateSetsEnricher(_enricher);
        }

        [Test]
        public void MarkPullRequestTitle_UpdateIsCorrect()
        {
            var updates = PackageUpdates.For(MakePackageFor("foo.bar", "1.1.0"));

            _enricher.Enrich(updates, _sut);
            var report = _sut.Output();

            Assert.That(report, Is.Not.Null);
            Assert.That(report, Is.Not.Empty);
            Assert.That(report, Is.EqualTo("Automatic update of foo.bar to 1.2.3"));
        }

        [Test]
        public void Output_MultipleUpdates_ReturnsNumberOfPackagesInTitle()
        {
            var updates = new[] {
                PackageUpdates.For(MakePackageFor("foo.bar", "1.1.0")),
                PackageUpdates.For(MakePackageFor("foo.bar", "1.1.5"))
            };

            _multiEnricher.Enrich(updates, _sut);
            var report = _sut.Output();

            Assert.That(report, Is.EqualTo("Automatic update of 2 packages"));
        }

        private static PackageInProject MakePackageFor(string packageName, string version)
        {
            var path = new PackagePath("c:\\temp", "folder\\src\\project1\\packages.config",
                PackageReferenceType.PackagesConfig);
            return new PackageInProject(packageName, version, path);
        }
    }
}
