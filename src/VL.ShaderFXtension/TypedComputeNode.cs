﻿using System;
using System.Collections.Generic;
using System.Linq;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
{
    public class TypedComputeNode<T> : GenericComputeNode<T, T>
    {
        public TypedComputeNode(Func<ShaderGeneratorContext, MaterialComputeColorKeys, ShaderClassCode> getShaderSource,
            IEnumerable<KeyValuePair<string, IComputeValue<T>>> inputs) : base(getShaderSource, inputs)
        {
        }
    }
}