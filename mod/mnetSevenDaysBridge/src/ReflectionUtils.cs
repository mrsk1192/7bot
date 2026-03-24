using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace mnetSevenDaysBridge
{
    /// <summary>
    /// Shared reflection and XUi dump utilities used by RespawnController
    /// and BridgeRuntimeBehaviour.
    /// </summary>
    internal static class ReflectionUtils
    {
        private static readonly BindingFlags MemberFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static object ReadMember(object target, string name)
        {
            if (target == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var type = target.GetType();
            var property = type.GetProperty(name, MemberFlags);
            if (property != null)
            {
                return property.GetValue(target, null);
            }

            var field = type.GetField(name, MemberFlags);
            return field == null ? null : field.GetValue(target);
        }

        public static object InvokeOptional(object target, string methodName, params object[] args)
        {
            if (target == null || string.IsNullOrWhiteSpace(methodName))
            {
                return null;
            }

            foreach (var method in target.GetType().GetMethods(MemberFlags))
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != args.Length)
                {
                    continue;
                }

                return method.Invoke(target, args);
            }

            return null;
        }

        public static float? TryReadFloat(object target, string name)
        {
            var raw = ReadMember(target, name);
            if (raw == null)
            {
                return null;
            }

            if (raw is float single)
            {
                return single;
            }

            if (raw is double @double)
            {
                return (float)@double;
            }

            return float.TryParse(
                Convert.ToString(raw, CultureInfo.InvariantCulture),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : (float?)null;
        }

        public static bool? TryReadBool(object target, string name)
        {
            var raw = ReadMember(target, name);
            if (raw == null)
            {
                return null;
            }

            if (raw is bool value)
            {
                return value;
            }

            return bool.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var parsed)
                ? parsed
                : (bool?)null;
        }

        /// <summary>
        /// Tries multiple member names in order, returning the first bool found.
        /// Used for compatibility across 7DTD versions with renamed members.
        /// </summary>
        public static bool? TryReadBool(object target, params string[] memberNames)
        {
            if (target == null)
            {
                return null;
            }

            foreach (var memberName in memberNames)
            {
                var raw = ReadMember(target, memberName) ?? InvokeOptional(target, memberName);
                if (raw == null)
                {
                    continue;
                }

                if (raw is bool boolValue)
                {
                    return boolValue;
                }

                if (bool.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        public static TEnum? TryReadEnum<TEnum>(object target, string name) where TEnum : struct
        {
            var raw = ReadMember(target, name);
            if (raw == null)
            {
                return null;
            }

            if (raw is TEnum value)
            {
                return value;
            }

            return Enum.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), true, out TEnum parsed)
                ? parsed
                : (TEnum?)null;
        }

        public static void DumpXUiToLogger(XUi xui, BridgeLogger logger)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== XUi Tree Dump ===");
            var windowGroups = ReadMember(xui, "WindowGroups") as IEnumerable;
            if (windowGroups == null)
            {
                sb.AppendLine("WindowGroups is null!");
            }
            else
            {
                foreach (var windowGroup in windowGroups)
                {
                    string id = ReadMember(windowGroup, "ID") as string ?? "unknown_id";
                    bool isShowing = TryReadBool(windowGroup, "isShowing") ?? false;
                    var controller = ReadMember(windowGroup, "Controller") as XUiController;
                    string ctlName = controller != null ? controller.GetType().Name : "null";
                    sb.AppendLine($"Group: {id} (isShowing={isShowing}) -> [Controller: {ctlName}]");
                    if (isShowing && controller != null)
                    {
                        DumpChildren(controller, sb, "  ");
                    }
                }
            }

            logger.Info(sb.ToString());
        }

        public static void DumpChildren(XUiController parent, StringBuilder sb, string indent)
        {
            IEnumerable children = null;
            foreach (var name in new[] { "Children", "childControllers", "m_ChildControllers", "children" })
            {
                children = ReadMember(parent, name) as IEnumerable;
                if (children != null)
                {
                    break;
                }
            }

            if (children == null)
            {
                return;
            }

            foreach (var childObj in children)
            {
                var child = childObj as XUiController;
                if (child == null)
                {
                    continue;
                }

                string id = ReadMember(child.ViewComponent, "ID") as string ?? "unknown_view_id";
                sb.AppendLine($"{indent}- Child: {id} [Controller: {child.GetType().Name}]");
                DumpChildren(child, sb, indent + "  ");
            }
        }
    }
}
