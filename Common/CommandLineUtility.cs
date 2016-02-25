using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Figaro.Utilities.Common;
using Figaro.Utilities.Resources;

namespace Figaro.Utilities
{
    class CommandLineUtility
    {
        private static Dictionary<string, string> _cachedResourceStrings;

        public static string GetLocalizedString(Type resourceType, string resourceName)
        {
            if (String.IsNullOrEmpty(resourceName))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "resourceName");
            }

            if (resourceType == null)
            {
                throw new ArgumentNullException("resourceType");
            }

            if (_cachedResourceStrings == null)
            {
                _cachedResourceStrings = new Dictionary<string, string>();
            }

            if (!_cachedResourceStrings.ContainsKey(resourceName))
            {
                PropertyInfo property = resourceType.GetProperty(resourceName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

                if (property == null)
                {
                    throw new InvalidOperationException(
                        String.Format(CultureInfo.CurrentCulture, DbxmlResources.ResourceTypeDoesNotHaveProperty, resourceType, resourceName));
                }

                if (property.PropertyType != typeof(string))
                {
                    throw new InvalidOperationException(
                        String.Format(CultureInfo.CurrentCulture, DbxmlResources.ResourcePropertyNotStringType, resourceName, resourceType));
                }

                MethodInfo getMethod = property.GetGetMethod(true);
                if ((getMethod == null) || (!getMethod.IsAssembly && !getMethod.IsPublic))
                {
                    throw new InvalidOperationException(
                        String.Format(CultureInfo.CurrentCulture, DbxmlResources.ResourcePropertyDoesNotHaveAccessibleGet, resourceType, resourceName));
                }

                _cachedResourceStrings[resourceName] = (string)property.GetValue(null, null);
            }

            return _cachedResourceStrings[resourceName];
        }
    }
}