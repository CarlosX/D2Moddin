﻿using System;
using Newtonsoft.Json.Linq;

namespace D2MPMaster.LiveData
{
    public static class DiffGenerator
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Generate a DiffSync JSON operation ($set update).
        /// </summary>
        /// <param name="source">The object that is changing.</param>
        /// <param name="fields">The updated fields.</param>
        /// <returns></returns>
        public static JObject Update<T>(this T source, string collection, string[] fields)
        {
            var obj = new JObject();
            obj["_o"] = "update";
            obj["_c"] = collection;
            obj["id"] = (string)source.GetType().GetProperty("id").GetValue(source, null);
            foreach (var field in fields)
            {
                try
                {
                    var prop = source.GetType().GetProperty(field);
                    if (prop == null) continue;
                    obj.Add(field, JToken.FromObject(Convert.ChangeType(prop.GetValue(source, null), prop.PropertyType)));
                }
                catch (Exception ex)
                {
                    log.Error("Can't generate UPDATE for field "+field+"", ex);
                }
            }
            return obj;
        }

        public static JObject Add<T>(this T source, string collection)
        {
            var obj = JObject.FromObject(source);
            obj["_o"] = "insert";
            obj["_c"] = collection;
            obj["id"] = (string)source.GetType().GetProperty("id").GetValue(source, null);
            obj.Remove("id");
            var type = source.GetType();
            return obj;
        }

        public static JObject Remove<T>(this T source, string collection)
        {
            var obj = new JObject();
            obj["_o"] = "remove";
            obj["_c"] = collection;
            obj["id"] = (string)source.GetType().GetProperty("id").GetValue(source, null);
            return obj;
        }

        public static JObject RemoveAll(string collection)
        {
            var obj = new JObject();
            obj["_o"] = "remove";
            obj["_c"] = collection;
            //This will make a empty delete specifier {}
            return obj;
        }
    }
}
