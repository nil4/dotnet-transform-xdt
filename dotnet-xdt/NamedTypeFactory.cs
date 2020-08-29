using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

namespace DotNet.Xdt
{
    class NamedTypeFactory
    {
        readonly string _relativePathRoot;
        readonly List<Registration> _registrations = new List<Registration>();

        internal NamedTypeFactory(string relativePathRoot)
        {
            _relativePathRoot = relativePathRoot;

            CreateDefaultRegistrations();
        }

        void CreateDefaultRegistrations() 
            => AddAssemblyRegistration(GetType().Assembly, GetType().Namespace);

        internal void AddAssemblyRegistration(Assembly assembly, string nameSpace) 
            => _registrations.Add(new Registration(assembly, nameSpace));

        internal void AddAssemblyRegistration(string assemblyName, string nameSpace) 
            => _registrations.Add(new AssemblyNameRegistration(assemblyName, nameSpace));

        internal void AddPathRegistration(string path, string nameSpace)
        {
            if (!Path.IsPathRooted(path))
            {
                // Resolve a relative path
                path = Path.Combine(Path.GetDirectoryName(_relativePathRoot), path);
            }

            _registrations.Add(new PathRegistration(path, nameSpace));
        }

        internal TObjectType? Construct<TObjectType>(string typeName) where TObjectType : class
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            Type? type = GetType(typeName);

            if (type is null)
                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_UnknownTypeName, typeName, typeof(TObjectType).Name));

            if (!type.IsSubclassOf(typeof(TObjectType)))
                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_IncorrectBaseType, type.FullName, typeof(TObjectType).Name));

            ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor is null)
                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_NoValidConstructor, type.FullName));

            return constructor.Invoke(Array.Empty<object>()) as TObjectType;
        }

        Type? GetType(string typeName)
        {
            Type? foundType = null;
            foreach (Registration registration in _registrations)
            {
                if (!registration.IsValid) continue;
                Type regType = registration.Assembly.GetType(string.Concat(registration.NameSpace, ".", typeName));
                if (regType is null) continue;
                if (foundType is null)
                    foundType = regType;
                else
                    throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_AmbiguousTypeMatch, typeName));
            }
            return foundType;
        }

        class Registration
        {
            public Registration(Assembly assembly, string nameSpace)
            {
                Assembly = assembly;
                NameSpace = nameSpace;
            }

            public bool IsValid => Assembly is not null;
            public string NameSpace { get; }
            public Assembly Assembly { get; }
        }

        class AssemblyNameRegistration : Registration
        {
            public AssemblyNameRegistration(string assemblyName, string nameSpace)
                : base(Assembly.Load(assemblyName), nameSpace)
            { }
        }

        class PathRegistration : Registration
        {
            public PathRegistration(string path, string nameSpace)
                : base(Assembly.LoadFile(path), nameSpace)
            { }
        }
    }
}
