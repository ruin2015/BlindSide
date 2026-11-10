// The game's lit shader for materials built in code: the toon shader (Blindside/Toon) when it is in the build — every converted
// material keeps it there — else URP/Lit. Same property names, so code that sets _BaseColor / _Smoothness / _Surface works either way.
using UnityEngine;

namespace Blindside.Rendering
{
    public static class ToonShader
    {
        public const string Name = "Blindside/Toon";
        static Shader cached;

        public static Shader Lit
        {
            get
            {
                if (cached == null) cached = Shader.Find(Name) ?? Shader.Find("Universal Render Pipeline/Lit");
                return cached;
            }
        }
    }
}
