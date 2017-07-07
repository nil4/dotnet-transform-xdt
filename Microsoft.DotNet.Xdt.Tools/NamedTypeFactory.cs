using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

namespace Microsoft.DotNet.Xdt.Tools
{
    internal class NamedTypeFactory
    {
        private readonly string _relativePathRoot;
        private readonly List<Registration> _registrations = new List<Registration>();

        internal NamedTypeFactory(string relativePathRoot)
        {
            _relativePathRoot = relativePathRoot;

            CreateDefaultRegistrations();
        }

        private void CreateDefaultRegistrations()
        {
            AddAssemblyRegistration(GetType().GetTypeInfo().Assembly, GetType().Namespace);
        }

        internal void AddAssemblyRegistration(Assembly assembly, string nameSpace)
        {
            _registrations.Add(new Registration(assembly, nameSpace));
        }

        internal void AddAssemblyRegistration(string assemblyName, string nameSpace)
        {
            _registrations.Add(new AssemblyNameRegistration(assemblyName, nameSpace));
        }

        internal void AddPathRegistration(string path, string nameSpace)
        {
            if (!Path.IsPathRooted(path))
            {
                // Resolve a relative path
                path = Path.Combine(Path.GetDirectoryName(_relativePathRoot), path);
            }

            _registrations.Add(new PathRegistration(path, nameSpace));
        }

        internal TObjectType Construct<TObjectType>(string typeName) where TObjectType : class
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            Type type = GetType(typeName);
            if (type == null)
            {
                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_UnknownTypeName, typeName, typeof(TObjectType).Name));
            }
            if (!type.GetTypeInfo().IsSubclassOf(typeof(TObjectType)))
            {
                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_IncorrectBaseType, type.FullName, typeof(TObjectType).Name));
            }
            ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_NoValidConstructor, type.FullName));
            }
            return constructor.Invoke(new object[0]) as TObjectType;
        }

        private Type GetType(string typeName)
        {
            Type foundType = null;
            foreach (Registration registration in _registrations)
            {
                if (!registration.IsValid) continue;
                Type regType = registration.Assembly.GetType(string.Concat(registration.NameSpace, ".", typeName));
                if (regType == null) continue;
                if (foundType == null)
                {
                    foundType = regType;
                }
                else
                {
                    throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_AmbiguousTypeMatch, typeName));
                }
            }
            return foundType;
        }

        private class Registration
        {
            public Registration(Assembly assembly, string nameSpace)
            {
                Assembly = assembly;
                NameSpace = nameSpace;
            }

            public bool IsValid => Assembly != null;

            public string NameSpace { get; }

            public Assembly Assembly { get; }
        }

        private class AssemblyNameRegistration : Registration
        {
            public AssemblyNameRegistration(string assemblyName, string nameSpace)
                : base(System.Reflection.Assembly.Load(new AssemblyName(assemblyName)), nameSpace)
            {
            }
        }

        private class PathRegistration : Registration
        {
            public PathRegistration(string path, string nameSpace)
                : base(Assembly.LoadFile(path), nameSpace)
            {
            }
        }
    }
}
