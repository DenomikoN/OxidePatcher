﻿using System;
using System.Collections.Generic;
using System.IO;

using OxidePatcher.Hooks;

using Newtonsoft.Json;

namespace OxidePatcher
{
    /// <summary>
    /// A set of changes to make to an assembly
    /// </summary>
    [JsonConverter(typeof(Converter))]
    public class Manifest
    {
        /// <summary>
        /// Gets or sets the name of the assembly in the target directory
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// Gets or sets the hooks contained in this project
        /// </summary>
        public List<Hook> Hooks { get; set; }

        /// <summary>
        /// Initializes a new instance of the Manifest class
        /// </summary>
        public Manifest()
        {
            // Fill in defaults
            Hooks = new List<Hook>();
        }

        public class Converter : JsonConverter
        {
            private struct HookRef
            {
                public string Type;
                public Hook Hook;
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Manifest);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                Manifest manifest = existingValue != null ? existingValue as Manifest : new Manifest();
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName) return null;
                    string propname = (string)reader.Value;
                    if (!reader.Read()) return null;
                    switch (propname)
                    {
                        case "AssemblyName":
                            manifest.AssemblyName = (string)reader.Value;
                            if (!Path.HasExtension(manifest.AssemblyName)) manifest.AssemblyName += ".dll";
                            break;
                        case "Hooks":
                            if (reader.TokenType != JsonToken.StartArray) return null;
                            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                            {
                                if (reader.TokenType != JsonToken.StartObject) return null;
                                if (!reader.Read()) return null;
                                if (reader.TokenType != JsonToken.PropertyName) return null;
                                if ((string)reader.Value != "Type") return null;
                                if (!reader.Read()) return null;
                                string hooktype = (string)reader.Value;
                                Type t = Hook.GetHookType(hooktype);
                                if (t == null) throw new Exception("Unknown hook type");
                                Hook hook = Activator.CreateInstance(t) as Hook;
                                if (!reader.Read()) return null;
                                if (reader.TokenType != JsonToken.PropertyName) return null;
                                if ((string)reader.Value != "Hook") return null;
                                if (!reader.Read()) return null;
                                serializer.Populate(reader, hook);
                                if (!reader.Read()) return null;

                                if (!Path.HasExtension(hook.AssemblyName)) hook.AssemblyName += ".dll";
                                manifest.Hooks.Add(hook);
                            }
                            break;
                    }
                }
                foreach (var hook in manifest.Hooks)
                {
                    if (!string.IsNullOrWhiteSpace(hook.BaseHookName))
                    {
                        foreach (var baseHook in manifest.Hooks)
                        {
                            if (baseHook.Name.Equals(hook.BaseHookName))
                            {
                                hook.BaseHook = baseHook;
                                break;
                            }
                        }
                        if (hook.BaseHook == null) throw new Exception("BaseHook missing: " + hook.BaseHookName);
                    }
                }
                return manifest;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                Manifest manifest = value as Manifest;

                writer.WriteStartObject();

                writer.WritePropertyName("AssemblyName");
                writer.WriteValue(Path.GetExtension(manifest.AssemblyName).Equals(".dll") ? Path.GetFileNameWithoutExtension(manifest.AssemblyName) : manifest.AssemblyName);

                HookRef[] refs = new HookRef[manifest.Hooks.Count];
                for (int i = 0; i < refs.Length; i++)
                {
                    refs[i].Hook = manifest.Hooks[i];
                    refs[i].Type = refs[i].Hook.GetType().Name;
                    refs[i].Hook.BaseHookName = refs[i].Hook.BaseHook != null ? refs[i].Hook.BaseHook.Name : null;
                }

                writer.WritePropertyName("Hooks");
                serializer.Serialize(writer, refs);

                writer.WriteEndObject();
            }
        }
    }
}
