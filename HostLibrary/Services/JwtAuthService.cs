using HostLibrary.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HostLibrary.Services
{
    public class JwtAuthService : IJwtAuthService
    {
        public readonly IConfiguration _configuration;
        public JwtAuthService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GetToken(string userName)
        {
            var claims = new List<Claim>
                {
                    new Claim(ClaimsIdentity.DefaultNameClaimType, userName)
                };

            ClaimsIdentity claimsIdentity =
            new ClaimsIdentity(claims, "Token", ClaimsIdentity.DefaultNameClaimType,
                ClaimsIdentity.DefaultRoleClaimType);

            JwtSecurityToken jwt;

            if (_configuration.GetSection("Project:Jwt:ValidateLifetime").Get<bool>())
            {
                // создаем JWT-токен
                jwt = new JwtSecurityToken(
                    issuer: _configuration.GetSection("Project:Jwt:Issuer").Get<string>(),
                    audience: _configuration.GetSection("Project:Jwt:Audience").Get<string>(),
                    notBefore: DateTime.UtcNow,
                    expires: DateTime.UtcNow.Add(TimeSpan.FromMinutes(_configuration.GetSection("Project:Jwt:Lifetime").Get<int>())),
                    claims: claimsIdentity.Claims,
                    signingCredentials: new SigningCredentials(
                        new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_configuration.GetSection("Project:Jwt:Key").Get<string>())),
                        SecurityAlgorithms.HmacSha256)
                    );
            }
            else
            {
                jwt = new JwtSecurityToken(
                    issuer: _configuration.GetSection("Project:Jwt:Issuer").Get<string>(),
                    audience: _configuration.GetSection("Project:Jwt:Audience").Get<string>(),
                    claims: claimsIdentity.Claims,
                    signingCredentials: new SigningCredentials(
                        new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_configuration.GetSection("Project:Jwt:Key").Get<string>())),
                        SecurityAlgorithms.HmacSha256)
                    );
            }

            return new JwtSecurityTokenHandler().WriteToken(jwt);
        }
    }
}