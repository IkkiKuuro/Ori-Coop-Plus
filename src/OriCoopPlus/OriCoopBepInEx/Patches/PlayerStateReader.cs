using System;
using System.Reflection;
using OriCoopBepInEx.Domain;
using UnityEngine;

namespace OriCoopBepInEx.Patches
{
    internal static class PlayerStateReader
    {
        public static PlayerSnapshot Read(object seinCharacter)
        {
            Component component = seinCharacter as Component;
            if (component == null)
            {
                return null;
            }

            Vector3 position = component.transform.position;
            PlayerSnapshot snapshot = new PlayerSnapshot();
            snapshot.Position = new Vector3Data(position.x, position.y, position.z);
            snapshot.Velocity = ReadVelocity(seinCharacter);
            snapshot.Animation.Name = ReadAnimationName(seinCharacter);
            snapshot.Animation.FacingLeft = ReadBool(seinCharacter, "FaceLeft");
            snapshot.Timestamp = DateTime.UtcNow.Ticks;
            return snapshot;
        }

        private static Vector2Data ReadVelocity(object instance)
        {
            object value = ReadMember(instance, "Velocity");
            if (value == null)
            {
                return new Vector2Data(0f, 0f);
            }

            Type type = value.GetType();
            return new Vector2Data(ReadFloat(value, type, "x"), ReadFloat(value, type, "y"));
        }

        private static string ReadAnimationName(object instance)
        {
            object animation = ReadMember(instance, "CurrentAnimation");
            if (animation == null)
            {
                return string.Empty;
            }
            object name = ReadMember(animation, "name");
            return name == null ? animation.ToString() : name.ToString();
        }

        private static bool ReadBool(object instance, string name)
        {
            object value = ReadMember(instance, name);
            return value is bool && (bool)value;
        }

        private static float ReadFloat(object instance, Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(float))
            {
                return (float)field.GetValue(instance);
            }
            return 0f;
        }

        private static object ReadMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(instance, null);
            }
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(instance);
        }
    }
}
