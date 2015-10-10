﻿using System;
using Core.Interfaces;
using Core.Services;
using Data.Entities;
using Data.Interfaces;
using Data.States;

using NUnit.Framework;
using StructureMap;
using UtilitiesTesting;
using UtilitiesTesting.Fixtures;
using Newtonsoft.Json;
using System.Collections.Generic;
using Data.Interfaces.DataTransferObjects;

namespace DockyardTest.Services
{
    
    [TestFixture]

    public class ProcessExecuteTests: BaseTest
    {
        private IProcess _process;

        [SetUp]
        //constructor method as it is run at the test start
        public override void SetUp()
        {
            base.SetUp();

            _process = ObjectFactory.GetInstance<IProcess>();
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public async void Execute_ProcessDoIsNull_ThrowsArgumentNullException()
        {
            await _process.Execute(null);
        }
        
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public async void Execute_ProcessDoCurrentActivityIsNull_ThrowsArgumentNullException()
        {
            //Get ProcessDO entity from static partial class FixtureData for already prepared data
            //The CurrentActivity value is already set to null and pass it immediately to service
            await _process.Execute(FixtureData.TestProcessCurrentActivityNULL());
        }


        [Test]
        public async void Execute_CurrentActivityStateIsActive_ExpectedException()
        {
            string crateStorage = GetCrateStorageAsString();

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var processDO = FixtureData.TestProcessExecute();
                var currAction = FixtureData.TestAction4();
                currAction.ActionState = ActionState.Active;
                currAction.CrateStorage = crateStorage;
                var nextAction = FixtureData.TestAction5();
                nextAction.CrateStorage = crateStorage;
                nextAction.ActionState = ActionState.Unstarted;
                processDO.CurrentActivity = currAction;
                processDO.NextActivity = nextAction;

                uow.ProcessRepository.Add(processDO);
                uow.ActivityRepository.Add(currAction);
                uow.ActivityRepository.Add(nextAction);

                uow.SaveChanges();
            }
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var processDO = uow.ProcessRepository.GetByKey(49);
                var ex = Assert.Throws<Exception>(async () => await _process.Execute(processDO));
                Assert.AreEqual("Action ID: 3 status is 4.", ex.Message);
            }
        }
        [Test]
        public async void Execute_CurrentActivityStateIsDeactive_ExpectedException()
        {
            string crateStorage = GetCrateStorageAsString();

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var processDO = FixtureData.TestProcessExecute();
                var currAction = FixtureData.TestAction4();
                currAction.ActionState = ActionState.Deactive;
                currAction.CrateStorage = crateStorage;
                var nextAction = FixtureData.TestAction5();
                nextAction.CrateStorage = crateStorage;
                nextAction.ActionState = ActionState.Unstarted;
                processDO.CurrentActivity = currAction;
                processDO.NextActivity = nextAction;

                uow.ProcessRepository.Add(processDO);
                uow.ActivityRepository.Add(currAction);
                uow.ActivityRepository.Add(nextAction);

                uow.SaveChanges();
            }
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var processDO = uow.ProcessRepository.GetByKey(49);
                var ex = Assert.Throws<Exception>(async () => await _process.Execute(processDO));
                Assert.AreEqual("Action ID: 3 status is 4.", ex.Message);
            }
        }
        [Test]
        public async void Execute_CurrentActivityStateIsError_ExpectedException()
        {
            string crateStorage = GetCrateStorageAsString();

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var processDO = FixtureData.TestProcessExecute();
                var currAction = FixtureData.TestAction4();
                currAction.ActionState = ActionState.Error;
                currAction.CrateStorage = crateStorage;
                var nextAction = FixtureData.TestAction5();
                nextAction.CrateStorage = crateStorage;
                nextAction.ActionState = ActionState.Unstarted;
                processDO.CurrentActivity = currAction;
                processDO.NextActivity = nextAction;

                uow.ProcessRepository.Add(processDO);
                uow.ActivityRepository.Add(currAction);
                uow.ActivityRepository.Add(nextAction);

                uow.SaveChanges();
            }
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var processDO = uow.ProcessRepository.GetByKey(49);
                var ex = Assert.Throws<Exception>(async () => await _process.Execute(processDO));
                Assert.AreEqual("Action ID: 3 status is 4.", ex.Message);
            }
        }
        [Test]
        public async void Execute_OneActivity_ShouldBeOk()
        {
            string crateStorage = GetCrateStorageAsString();

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var processDO = FixtureData.TestProcessExecute();
                var currAction = FixtureData.TestAction4();
                currAction.ActionState = ActionState.Unstarted;
                currAction.CrateStorage = crateStorage;
                var nextAction = FixtureData.TestAction5();
                nextAction.CrateStorage = crateStorage;
                nextAction.ActionState = ActionState.Unstarted;
                processDO.CurrentActivity = currAction;
                processDO.NextActivity = nextAction;

                uow.ProcessRepository.Add(processDO);
                uow.ActivityRepository.Add(currAction);
                uow.ActivityRepository.Add(nextAction);

                uow.SaveChanges();
            }
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var processDO = uow.ProcessRepository.GetByKey(49);
                await _process.Execute(processDO);

                Assert.IsNull(processDO.CurrentActivity);
                Assert.IsNull(processDO.NextActivity);
            }
        }
        [Test]
        public async void Execute_ManyActivities_ShouldBeOk()
        {
            string crateStorage = GetCrateStorageAsString();

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var processDO = FixtureData.TestProcessExecute();
                var currActivity = FixtureData.TestActionTreeWithActionTemplates();
                SetActionStateUnstarted(currActivity);
                processDO.CurrentActivity = currActivity;
                uow.ProcessRepository.Add(processDO);
                uow.ActivityRepository.Add(currActivity);

                uow.SaveChanges();
            }
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var processDO = uow.ProcessRepository.GetByKey(49);
                await _process.Execute(processDO);

                Assert.IsNull(processDO.CurrentActivity);
                Assert.IsNull(processDO.NextActivity);
            }
        }

        private static void SetActionStateUnstarted(ActivityDO currActivity)
        {
            if (currActivity == null)
                return;
            if (currActivity is ActionDO)
                (currActivity as ActionDO).ActionState = ActionState.Unstarted;
            if (currActivity.Activities != null)
            {
                foreach (var activity in currActivity.Activities)
                    SetActionStateUnstarted(activity);
            }
        }

        private static string GetCrateStorageAsString()
        {
            List<CrateDTO> curCratesDTO = FixtureData.TestCrateDTO1();
            CrateStorageDTO crateStorageDTO = new CrateStorageDTO();
            crateStorageDTO.CrateDTO.AddRange(curCratesDTO);
            string crateStorage = JsonConvert.SerializeObject(crateStorageDTO);
            return crateStorage;
        }	

    }

}

