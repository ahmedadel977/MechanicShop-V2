using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Common;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public class WebAppFactoryCollection : ICollectionFixture<WebAppFactory>
{
    public const string CollectionName = "WebAppFactoryCollection";
}
