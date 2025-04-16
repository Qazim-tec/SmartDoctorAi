using HealthTech.DTOs;

namespace HealthTech.IService
{
    public interface IAuthService
    {
        Task<UserInfoDto> RegisterAsync(RegisterDto registerDto);
        Task<UserInfoDto> LoginAsync(LoginDto loginDto);
    }
}
