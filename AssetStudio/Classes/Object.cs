﻿using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;

namespace AssetStudio
{
    public class Object
    {
        public SerializedFile assetsFile;
        public ObjectReader reader;
        public long m_PathID;
        public int[] version;
        protected BuildType buildType;
        public BuildTarget platform;
        public ClassIDType type;
        public SerializedType serializedType;
        public uint byteSize;

        public Object() { }

        public Object(ObjectReader reader)
        {
            this.reader = reader;
            reader.Reset();
            assetsFile = reader.assetsFile;
            type = reader.type;
            m_PathID = reader.m_PathID;
            version = reader.version;
            buildType = reader.buildType;
            platform = reader.platform;
            serializedType = reader.serializedType;
            byteSize = reader.byteSize;

            if (platform == BuildTarget.NoTarget)
            {
                var m_ObjectHideFlags = reader.ReadUInt32();
            }
        }

        public string Dump()
        {
            if (serializedType?.m_Type != null)
            {
                return TypeTreeHelper.ReadTypeString(serializedType.m_Type, reader);
            }
            return null;
        }

        public string Dump(TypeTree m_Type)
        {
            if (m_Type != null)
            {
                return TypeTreeHelper.ReadTypeString(m_Type, reader);
            }
            return null;
        }

        public string DumpObject()
        {
            string str = null;
            try
            {
                str = JsonConvert.SerializeObject(this, new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    ContractResolver = new IgnorePropertiesResolver()
                }).Replace("  ", "    ");
            }
            catch
            {
                //ignore
            }
            return str;
        }

        public OrderedDictionary ToType()
        {
            if (serializedType?.m_Type != null)
            {
                return TypeTreeHelper.ReadType(serializedType.m_Type, reader);
            }
            return null;
        }

        public OrderedDictionary ToType(TypeTree m_Type)
        {
            if (m_Type != null)
            {
                return TypeTreeHelper.ReadType(m_Type, reader);
            }
            return null;
        }

        public byte[] GetRawData()
        {
            reader.Reset();
            return reader.ReadBytes((int)byteSize);
        }

        private class IgnorePropertiesResolver : DefaultContractResolver
        {
            private static readonly HashSet<string> _ignoreProps;

            static IgnorePropertiesResolver()
            {
                _ignoreProps = new HashSet<string> { "assetsFile", "reader", "version", "platform", "serializedType" };
            }

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);
                if (_ignoreProps.Contains(property.PropertyName))
                {
                    property.ShouldSerialize = _ => false;
                }
                return property;
            }
        }
    }
}
