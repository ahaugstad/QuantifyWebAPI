using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace QuantifyWebAPI.Controllers
{
    public class ProtoTypeController : ApiController
    {
        // GET: api/ProtoType
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/ProtoType/5
        public string Get(int id)
        {
            return "value";
        }

        // POST: api/ProtoType
        public void Post([FromBody]string value)
        {
        }

        // PUT: api/ProtoType/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/ProtoType/5
        public void Delete(int id)
        {
        }
    }
}
