﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using Data.Entities;
using Data.Infrastructure;

namespace Web.Controllers
{
    public class ProcessController : ApiController
    {
        private DockyardDbContext db = new DockyardDbContext();

        // GET: api/Process
        public IQueryable<ProcessDO> Get()
        {
            return db.Processes;
        }

        

        // GET: api/Process/5
        [ResponseType(typeof(ProcessDO))]
        public IHttpActionResult GetProcess(int id)
        {
            ProcessDO processDO = db.Processes.Find(id);
            if (processDO == null)
            {
                return NotFound();
            }

            return Ok(processDO);
        }

        // PUT: api/Process/5
        [ResponseType(typeof(void))]
        public IHttpActionResult PutProcess(int id, ProcessDO processDO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != processDO.Id)
            {
                return BadRequest();
            }

            db.Entry(processDO).State = EntityState.Modified;

            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProcessDOExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        // POST: api/Process
        [ResponseType(typeof(ProcessDO))]
        public IHttpActionResult PostProcessDO(ProcessDO processDO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            db.Processes.Add(processDO);
            db.SaveChanges();

            return CreatedAtRoute("DefaultApi", new { id = processDO.Id }, processDO);
        }

        // DELETE: api/Process/5
        [ResponseType(typeof(ProcessDO))]
        public IHttpActionResult DeleteProcessDO(int id)
        {
            ProcessDO processDO = db.Processes.Find(id);
            if (processDO == null)
            {
                return NotFound();
            }

            db.Processes.Remove(processDO);
            db.SaveChanges();

            return Ok(processDO);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool ProcessDOExists(int id)
        {
            return db.Processes.Count(e => e.Id == id) > 0;
        }
    }
}