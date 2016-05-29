using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Runtime.Loader;

namespace Microsoft.DotNet.Xdt.Tools
{
    internal class NamedTypeFactory
    {
        private readonly string _relativePathRoot;
        private readonly List<Registration> _registrations = new List<Registration>();

        internal NamedTypeFactory(string relativePathRoot) {
            _relativePathRoot = relativePathRoot;

            CreateDefaultRegistrations();
        }

        private void CreateDefaultRegistrations() {
            AddAssemblyRegistration(GetType().GetTypeInfo().Assembly, GetType().Namespace);
        }

        internal void AddAssemblyRegistration(Assembly assembly, string nameSpace) {
            _registrations.Add(new Registration(assembly, nameSpace));
        }

        internal void AddAssemblyRegistration(string assemblyName, string nameSpace) {
            _registrations.Add(new AssemblyNameRegistration(assemblyName, nameSpace));
        }

        internal void AddPathRegistration(string path, string nameSpace) {
            if (!Path.IsPathRooted(path)) {
                // Resolve a relative path
                path = Path.Combine(Path.GetDirectoryName(_relativePathRoot), path);
            }

            _registrations.Add(new PathRegistration(path, nameSpace));
        }

        internal ObjectType Construct<ObjectType>(string typeName) where ObjectType : class {
            if (string.IsNullOrEmpty(typeName)) return null;
            Type type = GetType(typeName);
            if (type == null) {
                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture,SR.XMLTRANSFORMATION_UnknownTypeName, typeName, typeof(ObjectType).Name));
            }
            if (!type.GetTypeInfo().IsSubclassOf(typeof(ObjectType))) {
                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture,SR.XMLTRANSFORMATION_IncorrectBaseType, type.FullName, typeof(ObjectType).Name));
            }
            ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null) {
                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture,SR.XMLTRANSFORMATION_NoValidConstructor, type.FullName));
            }
            return constructor.Invoke(new object[0]) as ObjectType;
        }

        private Type GetType(string typeName) {
            Type foundType = null;
            foreach (Registration registration in _registrations) {
                if (!registration.IsValid) continue;
                Type regType = registration.Assembly.GetType(string.Concat(registration.Namespace, ".", typeName));
                if (regType == null) continue;
                if (foundType == null) {
                    foundType = regType;
                }
                else {
                    throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture,SR.XMLTRANSFORMATION_AmbiguousTypeMatch, typeName));
                }
            }
            return foundType;
        }

        private class Registration
        {
            public Registration(Assembly assembly, string @namespace) {
                Assembly = assembly;
                Namespace = @namespace;
            }

            public bool IsValid => Assembly != null;
            public string Namespace { get; }
            public Assembly Assembly { get; }
        }

        private class AssemblyNameRegistration : Registration
        {
            public AssemblyNameRegistration(string assemblyName, string @namespace)
                : base(Assembly.Load(new AssemblyName(assemblyName)), @namespace) {
            }
        }

        private class PathRegistration : Registration
        {
            public PathRegistration(string path, string @namespace)
                : base(AssemblyLoadContext.Default.LoadFromAssemblyPath(path), @namespace) {
            }
        }
    }
}
