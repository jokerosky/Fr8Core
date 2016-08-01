﻿using System.Collections.Generic;
using System.Linq;
using System.Web.Http.Results;
using NUnit.Framework;
using StructureMap;
using Data.Entities;
using Data.Interfaces;
using Data.States;
using Fr8.Infrastructure.Data.DataTransferObjects;
using HubWeb.Controllers;
using Fr8.Testing.Unit.Fixtures;
using HubTests.Controllers.Api;

namespace HubTests.Controllers
{
    [TestFixture]
    [Category("UserController")]
    public class UserControllerTests : ApiControllerTestBase
    {
        private Fr8AccountDO _testAccount1;
        private Fr8AccountDO _testAccount3;

        public override void SetUp()
        {
            base.SetUp();
            InitializeRoles();
            InitializeUsers();
        }

        [Test]
        public void Get()
        {
            var controller = CreateController<UsersController>();

            var result = controller.Get() as OkNegotiatedContentResult<List<UserDTO>>;
                
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Content);

            Assert.AreEqual(result.Content.Count, 3);
            Assert.AreEqual(result.Content[0].Id, _testAccount1.Id);
            Assert.AreEqual(result.Content[2].Id, _testAccount3.Id);
        }

        [Test]
        public void GetById()
        {
            var controller = CreateController<UsersController>();

            var result = controller.UserData(_testAccount3.Id) as OkNegotiatedContentResult<UserDTO>;

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Content);
            Assert.AreEqual(result.Content.EmailAddress, _testAccount3.EmailAddress.Address);
        }

        private void InitializeUsers()
        {
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                _testAccount1 = FixtureData.TestUser1();
                uow.UserRepository.Add(_testAccount1);

                _testAccount3 = FixtureData.TestUser3();
                uow.UserRepository.Add(_testAccount3);

                uow.SaveChanges();
            }
        }

        private void InitializeRoles()
        {
            CreateRole(Roles.Admin);
            CreateRole(Roles.Customer);
        }

        private static void CreateRole(string roleName)
        {
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                if (!uow.AspNetRolesRepository.GetQuery().Any(r => r.Name == roleName))
                {
                    //Create a role
                    uow.AspNetRolesRepository.Add(new AspNetRolesDO { Name = roleName });
                    uow.SaveChanges();
                }
            }
        }
    }
}
