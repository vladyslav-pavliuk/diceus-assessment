using AutoMapper;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.Application.Users;

internal sealed class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<User, UserDto>();
    }
}
