using System;

namespace THMI_Mod_Manager.Services
{
    /// <summary>
    /// Mod access control attribute.
    /// Marks methods that can be called by mods.
    /// / Mod 访问控制特性，用于标记允许 Mod 调用的方法
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ModAccessAttribute : Attribute
    {
        public string Description { get; set; } = "";

        public ModAccessAttribute()
        {
        }

        public ModAccessAttribute(string description)
        {
            Description = description;
        }
    }
}
