﻿using CommonLayer.RequestModels;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RepositoryLayer.Context;
using RepositoryLayer.Entity;
using RepositoryLayer.Interfaces;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace RepositoryLayer.Services
{
    public class UserRepository : IUserRepository
    {
        private readonly FunNoteContext context;
        private readonly IConfiguration config;

        private readonly BcryptEncryption bcrypt;

        public UserRepository(FunNoteContext context, IConfiguration config)
        {
            this.context = context;
            this.config = config;
            this.bcrypt = new BcryptEncryption();
        }

        public UserEntity UserRegistration(RegisterModel model)
        {
            var user = context.UserTable.FirstOrDefault(x=>x.Useremail == model.Useremail);
            if(user != null)
            {
                throw new Exception("User Already Exist");
            }

            UserEntity entity = new UserEntity
            {
                Firstname = model.Firstname,
                Lastname = model.Lastname,
                Useremail = model.Useremail,
                Userpassword = bcrypt.HashPassGenerator(model.Userpassword),
                CreateTime = DateTime.Now,
                LastLoginTime = DateTime.Now
            };

            context.UserTable.Add(entity);
            context.SaveChanges();

            return entity;
        }

        public string UserLogin(LoginModel model)
        {
            UserEntity user = context.UserTable.FirstOrDefault(x => x.Useremail == model.useremail);

            if (user != null)
            {
                if (bcrypt.MatchPass(model.userpassword, user.Userpassword))
                {
                    user.LastLoginTime = DateTime.Now;
                    string token = GenerateToken(user.Useremail, user.UserId);
                    return token;
                }
                else
                {
                    throw new ArgumentException("Incorrect password");
                }
            }
            else
            {
                throw new ArgumentException("Incorrect email");
            }
        }

        private string GenerateToken(string Email, int userId)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim("UserEmail", Email),
                new Claim("UserId", userId.ToString())
            };

            var token = new JwtSecurityToken(
                config["Jwt:Issuer"],
                config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(15),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public ForgotPasswordModel ForgotPassword(string email)
        {
            var entity = context.UserTable.FirstOrDefault(user => user.Useremail == email);

            if (entity != null)
            {
                ForgotPasswordModel model = new ForgotPasswordModel
                {
                    UserId = entity.UserId,
                    UserEmail = entity.Useremail,
                    Token = GenerateToken(email, entity.UserId)
                };

                return model;
            }
            throw new ArgumentException("User with the specified email does not exist");
        }

        public string ResetPassword(string email, ResetPasswordModel model)
        {
            if (model.new_password == model.confirm_password)
            {
                if (CheckEmail(email))
                {
                    var entity = context.UserTable.SingleOrDefault(user => user.Useremail == email);
                    entity.Userpassword = bcrypt.HashPassGenerator(model.new_password);
                    context.SaveChanges();
                    return "true";
                }
                throw new ArgumentException("User with the specified email does not exist");
            }
            throw new ArgumentException("Password does not match");
        }


        public bool CheckEmail(string email)
        {
            var user = context.UserTable.SingleOrDefault(user => user.Useremail == email);
            return user != null;
        }

        
    }
}
