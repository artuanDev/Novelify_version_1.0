using System.Collections.Generic;

namespace Novelify
{
    // Unity requires an imported ScriptableObject type to live in a source file
    // with the same name. Keeping this separate is required for AddObjectToAsset.
    public class RuntimeNovelFunction : RuntimeNovelGraph
    {
        public List<RuntimeFunctionInput> Inputs = new List<RuntimeFunctionInput>();
        public List<RuntimeFunctionOutput> Outputs = new List<RuntimeFunctionOutput>();
    }
}
