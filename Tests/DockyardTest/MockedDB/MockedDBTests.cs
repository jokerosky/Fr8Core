﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Data.Entities;
using Data.Infrastructure.StructureMap;
using Data.Interfaces;
using Data.States;
using NUnit.Framework;
using StructureMap;
using UtilitiesTesting;
using UtilitiesTesting.Fixtures;

namespace DockyardTest.MockedDB
{
    [TestFixture]
    public class MockedDBTests : BaseTest
    {
        //This test is to ensure our mocking properly distinguishes between saved and local DbSets (to mimic EF behaviour)
        [Test]
        public void TestDBMocking()
        {
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var curUser = new FixtureData(uow).TestUser1();
                curUser.Id = "1";
               
                uow.UserRepository.Add(curUser);

                using (var subUow = ObjectFactory.GetInstance<IUnitOfWork>())
                {
                    Assert.AreEqual(0, subUow.UserRepository.GetQuery().Count());
                    Assert.AreEqual(0, subUow.UserRepository.DBSet.Local.Count());
                }

                Assert.AreEqual(0, uow.UserRepository.GetQuery().Count());
                Assert.AreEqual(1, uow.UserRepository.DBSet.Local.Count());

                uow.SaveChanges();

                using (var subUow = ObjectFactory.GetInstance<IUnitOfWork>())
                {
                    Assert.AreEqual(1, subUow.UserRepository.GetQuery().Count());
                    Assert.AreEqual(0, subUow.UserRepository.DBSet.Local.Count());
                }
            }
        }

        [Test]
        public void TestDBMockingForeignKeyUpdate()
        {
            //using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            //{
            //    var user = new UserDO();
            //    user.State = UserState.Active;
            //    var curUser = new FixtureData(uow).TestBookingRequest1();
            //    curUser.Id = 1;
            //    curUser.CustomerID = user.Id;
            //    uow.UserRepository.Add(curUser);
            //    uow.UserRepository.Add(user);

            //    uow.SaveChanges();

            //    Assert.NotNull(curUser.Customer);
            //}
        }

        [Test]
        public void TestCollectionsProperlyUpdated()
        {
            ////Force a seed -- helps with debug
            //using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            //{
            //    var userDO = uow.UserRepository.GetOrCreateUser("testemail@gmail.com");
            //    uow.UserRepository.Add(userDO);

            //    ObjectFactory.GetInstance<ISecurityServices>().Login(uow, userDO);

            //    var negDO = new FixtureData(uow).TestNegotiation1();
            //    negDO.Id = 1;
            //    uow.NegotiationsRepository.Add(negDO);

            //    var attendee = new AttendeeDO();
            //    attendee.NegotiationID = 1;
            //    uow.AttendeeRepository.Add(attendee);

            //    uow.SaveChanges();

            //    Assert.AreEqual(1, negDO.Attendees.Count);
            //}

            //using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            //{
            //    var negDO = uow.NegotiationsRepository.GetQuery().First();

            //    Assert.AreEqual(1, negDO.Attendees.Count);
            //}
        }


        [Test]
        public void AssertAllTestsImplementBaseTest()
        {
            var failedTypes = new List<Type>();
                foreach (var testClass in GetType().Assembly.GetTypes().Where(t => t.GetCustomAttributes<TestFixtureAttribute>().Any()))
                {
                    if (testClass != typeof(BaseTest) && !testClass.IsSubclassOf(typeof(BaseTest)))
                        failedTypes.Add(testClass);
                }
                var exceptionMessages = new List<String>();
                foreach (var failedType in failedTypes)
                {
                    var testClassName = failedType.Name;
                    exceptionMessages.Add(testClassName + " must implement 'BaseTest'");
                }
                if (exceptionMessages.Any())
                    Assert.Fail(String.Join(Environment.NewLine, exceptionMessages));
            }

            //[Test, ExpectedException(ExpectedMessage = "Foreign row does not exist.\nValue '0' on 'NegotiationDO.NegotiationState' pointing to '_NegotiationStateTemplate.Id'")]
            //public void TestForeignKeyEnforced()
            //{
            //    using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            //    {
            //        var br = new FixtureData(uow).TestBookingRequest1();
            //        var negDO = new NegotiationDO {Id = 1};
            //        negDO.NegotiationState = 0;
            //        negDO.BookingRequest = br;
            //        uow.NegotiationsRepository.Add(negDO);

            //        uow.SaveChanges();
            //    }
            //}
        }
    }

