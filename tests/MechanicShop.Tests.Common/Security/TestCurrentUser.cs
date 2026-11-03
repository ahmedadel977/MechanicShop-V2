using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Infrastructure.identity;

namespace MechanicShop.Tests.Common.Security;

public class TestCurrentUser : IUser
{
    private AppUser? _currentUser;

    public void Returns(AppUser currentUser)
    {
        _currentUser = currentUser;
    }

    public string? Id => _currentUser!.Id ?? UserFactory.CreateUser().Id;
}