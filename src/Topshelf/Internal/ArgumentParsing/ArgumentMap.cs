// Copyright 2007-2008 The Apache Software Foundation.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//   http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace Topshelf.Internal.ArgumentParsing
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using System.Text;

    public class ArgumentMap :
        IArgumentMap
    {
        private readonly Dictionary<string, ArgumentTarget> _namedArgs = new Dictionary<string, ArgumentTarget>(StringComparer.OrdinalIgnoreCase);
        private readonly Type _type;
        private readonly List<ArgumentTarget> _unnamedArgs = new List<ArgumentTarget>();

        public ArgumentMap(Type type)
        {
            _type = type;

            foreach (PropertyInfo property in _type.GetProperties())
            {
                object[] attributes = property.GetCustomAttributes(typeof (ArgumentAttribute), true);
                if (attributes.Length == 1)
                {
                    ArgumentAttribute attribute = (ArgumentAttribute) attributes[0];
                    DefaultArgumentAttribute defaultAttribute = attribute as DefaultArgumentAttribute;

                    ArgumentTarget arg = (defaultAttribute == null)
                                             ? new ArgumentTarget(attribute, property)
                                             : new DefaultArgumentTarget(attribute, property);

                    if (string.IsNullOrEmpty(arg.Attribute.Key))
                        _unnamedArgs.Add(arg);
                    else
                        _namedArgs.Add(arg.Attribute.Key, arg);
                }
            }
        }

        #region IArgumentMap Members

        public string Usage
        {
            get
            {
                StringBuilder sb = new StringBuilder();

                List<ArgumentTarget> allArgs = new List<ArgumentTarget>();
                allArgs.AddRange(_namedArgs.Values);
                allArgs.AddRange(_unnamedArgs);

                foreach (ArgumentTarget target in allArgs)
                {
                    if (target is DefaultArgumentTarget)
                    {
                        sb.AppendFormat("{1}<{0}>{2} ", target.Property.Name, target.Attribute.Required ? "" : "[",
                                        target.Attribute.Required ? "" : "]");
                    }
                    else if (target.Attribute.Required)
                        sb.AppendFormat("-{0} ", target.Attribute.Key);
                    else
                        sb.AppendFormat("[-{0}] ", target.Attribute.Key);
                }

                sb.Append(Environment.NewLine + Environment.NewLine);

                foreach (ArgumentTarget target in allArgs)
                {
                    if (target is DefaultArgumentTarget)
                    {
                        sb.AppendFormat("{0,-20}{1}" + Environment.NewLine, "<" + target.Property.Name + ">",
                                        target.Attribute.Description);
                    }
                    else
                    {
                        sb.AppendFormat("{0,-20}{1}" + Environment.NewLine, target.Attribute.Key,
                                        target.Attribute.Description);
                    }
                }

                return sb.ToString();
            }
        }

        #endregion

        public IEnumerable<IArgument> ApplyTo(object objectToApplyTo, IEnumerable<IArgument> arguments, ArgumentIntercepter intercepter)
        {
            List<ArgumentTarget> usedTargets = new List<ArgumentTarget>();
            foreach (KeyValuePair<string, ArgumentTarget> namedArg in _namedArgs)
            {
                usedTargets.Add(namedArg.Value);
            }
            foreach (ArgumentTarget unnamedArg in _unnamedArgs)
            {
                usedTargets.Add(unnamedArg);
            }

            List<IArgument> unused = new List<IArgument>();

            int unnamedIndex = 0;

            foreach (IArgument arg in arguments)
            {
                if (string.IsNullOrEmpty(arg.Key))
                {
                    if (unnamedIndex < _unnamedArgs.Count && intercepter(_unnamedArgs[unnamedIndex++].Property.Name, arg.Value))
                    {
                        usedTargets.Remove(_unnamedArgs[unnamedIndex]);
                        ApplyValueToProperty(_unnamedArgs[unnamedIndex++].Property, objectToApplyTo, arg.Value);
                    }
                    else
                    {
                        unused.Add(arg);
                    }
                }
                else if (_namedArgs.ContainsKey(arg.Key) && intercepter(_namedArgs[arg.Key].Property.Name, arg.Value))
                {
                    usedTargets.Remove(_namedArgs[arg.Key]);
                    ApplyValueToProperty(_namedArgs[arg.Key].Property, objectToApplyTo, arg.Value);
                }
                else
                {
                    unused.Add(arg);
                }
            }

            foreach (ArgumentTarget target in usedTargets)
            {
                if (target.Attribute.Required)
                    throw new ArgumentParsingException("Argument " + (target.Attribute.Key ?? target.Property.Name) + " is required");
            }

            return unused;
        }

        public IEnumerable<IArgument> ApplyTo(object objectToApplyTo, IEnumerable<IArgument> arguments)
        {
            return ApplyTo(objectToApplyTo, arguments, delegate { return true; });
        }

        public void ApplyValueToProperty(PropertyInfo property, object objectToApplyTo, string argumentValue)
        {
            TypeMatchCheck(property, objectToApplyTo);

            object value;

            if (property.PropertyType == typeof (bool))
            {
                value = bool.Parse(argumentValue);
            }
            else
            {
                value = argumentValue;
            }

            try
            {
                property.SetValue(objectToApplyTo, value, BindingFlags.Public | BindingFlags.Instance, null, null, CultureInfo.InvariantCulture);    
            }
            catch(Exception ex)
            {
                string message = string.Format("Error setting property {0} on object '{1}' with value '{2}'", property.Name, objectToApplyTo.GetType().Name, value);
                throw new ArgumentParsingException(message, ex);
            }
			
        }

        private void TypeMatchCheck(PropertyInfo property, object objectToApplyTo)
        {
            if(!property.DeclaringType.Equals(objectToApplyTo.GetType()))
            {
                string message = string.Format("You are trying to set the property '{0}' on the the type '{1}' but you gave the program an object of type '{2}' to set. Check the 'objectToApplyTo' parameter", property.Name, property.DeclaringType.Name, objectToApplyTo.GetType().Name);
                throw new ArgumentParsingException(message);
            }
        }
    }
}