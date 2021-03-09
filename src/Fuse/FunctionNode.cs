﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;

namespace Fuse
{
    public abstract class AbstractFunctionNode<T> : ShaderNode<T>
    {
        protected AbstractFunctionNode(IEnumerable<AbstractGpuValue> theArguments, string theFunction, ConstantValue<T> theDefault) : base(theFunction, theDefault)
        {
            Setup(theArguments, new Dictionary<string, string> {{"function", theFunction}});
        }


        protected override string SourceTemplate()
        {
            return "${resultType} ${resultName} = ${function}(${arguments});";
        }
        
    }
    
    public class IntrinsicFunctionNode<T> : AbstractFunctionNode<T>
    {
        
        public IntrinsicFunctionNode(IEnumerable<AbstractGpuValue> theArguments, string theFunction, ConstantValue<T> theDefault) : base(theArguments, theFunction, theDefault)
        {
            
        }
    }
    
    public class MixinFunctionNode<T> : AbstractFunctionNode<T>
    {

        public MixinFunctionNode(IEnumerable<AbstractGpuValue> theArguments, string theFunction, ConstantValue<T> theDefault, string theMixin) : base(theArguments,theFunction, theDefault)
        {
            MixIns = new List<string>(){theMixin};
        }

        public sealed override List<string> MixIns { get; }
    }
    
    public sealed class CustomFunctionNode<T>: AbstractFunctionNode<T>
    {
        
        public CustomFunctionNode(
            IEnumerable<AbstractGpuValue> theArguments, 
            string theFunction, 
            string theCodeTemplate, 
            ConstantValue<T> theDefault, 
            IEnumerable<IDelegateNode> theDelegates = null, 
            IEnumerable<string> theMixins = null, 
            IDictionary<string,string> theFunctionValues = null
        ) : base(theArguments, theFunction, theDefault)
        {
            MixIns = new List<string>();
            if(theMixins!=null)MixIns.AddRange(theMixins);
            Functions = new Dictionary<string, string>();
            Declarations = new List<string>();
            Inputs = new List<IGpuInput>();
            
            var signature = theFunction + GetHashCode();

            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetGpuTypeForType<T>()},
                {"signature", signature}
            };

            var inputs = theArguments.ToList();
            Ins = inputs;
            HandleDelegates(theDelegates,functionValueMap);

            theCodeTemplate = ShaderNodesUtil.IndentCode(theCodeTemplate);
            theFunctionValues?.ForEach(kv => functionValueMap.Add(kv.Key, kv.Value));
            Functions.Add(signature, ShaderNodesUtil.Evaluate(theCodeTemplate, functionValueMap) + Environment.NewLine);
            Setup(inputs, new Dictionary<string, string> {{"function", signature}});
        }
        
        private void HandleDelegates(IEnumerable<IDelegateNode> theDelegates, IDictionary<string, string> theFunctionValueMap)
        {
            theDelegates?.ForEach(delegateNode =>
            {
                if (delegateNode == null) return;
                
                theFunctionValueMap.Add(delegateNode.Name, delegateNode.FunctionName);
                delegateNode.Functions.ForEach(kv => Functions[kv.Key] = kv.Value);
                MixIns.AddRange(delegateNode.MixIns);
                Declarations.AddRange(delegateNode.Declarations);
                Inputs.AddRange(delegateNode.Inputs);
            });
        }
        
        public override IDictionary<string, string> Functions { get; }
        public override List<string> MixIns { get; }
        public override List<string> Declarations { get; }
        public override List<IGpuInput> Inputs { get; }
    }
    
    public sealed class PatchedFunctionParameter<T> : ShaderNode<T> 
    {

        public PatchedFunctionParameter(GpuValue<T> theType, int theId = 0): base("argument", null,"argument")
        {
            Output = new DelegateValue<T>("val" + GetHashCode())
            {
                ParentNode = this
            };
            Ins = new List<AbstractGpuValue>();
            Output.name = "arg_"+theId;
        }

        public string TypeName()
        {
            return TypeHelpers.GetGpuTypeForType<T>();
        }

        public string Name()
        {
            return Output.ID;
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }
    
    public sealed class PatchedFunctionNode<T>: AbstractFunctionNode<T>
    {
        
        public PatchedFunctionNode(
            IEnumerable<AbstractGpuValue> theArguments, 
            AbstractGpuValue theFunction,
            string theName,
            ConstantValue<T> theDefault, 
            IEnumerable<IDelegateNode> theDelegates = null, 
            IEnumerable<string> theMixins = null, 
            IDictionary<string,string> theFunctionValues = null
        ) : base(theArguments, "patchedFunction", theDefault)
        {
            MixIns = new List<string>();
            if(theMixins!=null)MixIns.AddRange(theMixins);
            Functions = new Dictionary<string, string>();
            Declarations = new List<string>();
            Inputs = new List<IGpuInput>();
            
            var signature = theName + BuildSignature(theArguments)  +"To" + TypeHelpers.GetShaderTypeForType<T>();

            
            
            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetGpuTypeForType<T>()},
                {"functionName", signature},
                {"arguments", BuildArguments(theArguments)},
                {"functionImplementation", theFunction.ParentNode.BuildSourceCode()},
                {"result", theFunction.ID}
            };
            
            const string functionCode = @"    ${resultType} ${functionName}(${arguments}){
${functionImplementation}
        return ${result};
    }";

            var inputs = theArguments.ToList();
            Ins = inputs;
            HandleDelegates(theDelegates,functionValueMap);

            Functions.Add(signature, ShaderNodesUtil.Evaluate(functionCode, functionValueMap) + Environment.NewLine);
            Setup(inputs, new Dictionary<string, string> {{"function", signature}});
            
        }
        
        private void HandleDelegates(IEnumerable<IDelegateNode> theDelegates, IDictionary<string, string> theFunctionValueMap)
        {
            theDelegates?.ForEach(delegateNode =>
            {
                if (delegateNode == null) return;
                
                theFunctionValueMap.Add(delegateNode.Name, delegateNode.FunctionName);
                delegateNode.Functions.ForEach(kv => Functions[kv.Key] = kv.Value);
                MixIns.AddRange(delegateNode.MixIns);
                Declarations.AddRange(delegateNode.Declarations);
                Inputs.AddRange(delegateNode.Inputs);
            });
        }
        
        private static string BuildSignature(IEnumerable<AbstractGpuValue> inputs)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input => stringBuilder.Append(TypeHelpers.GetShaderTypeForType(input.GetType().GetGenericArguments()[0])));
            return stringBuilder.ToString();
        }
        
        private static string BuildArguments(IEnumerable<AbstractGpuValue> inputs)
        {
            var stringBuilder = new StringBuilder();
            var c = 0;
            inputs.ForEach(input =>
            {
                stringBuilder.Append(input.TypeName());
                stringBuilder.Append(" ");
                stringBuilder.Append("arg_"+c);
                stringBuilder.Append(", ");
                c++;
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }
        
        public override IDictionary<string, string> Functions { get; }
        public override List<string> MixIns { get; }
        public override List<string> Declarations { get; }
        public override List<IGpuInput> Inputs { get; }
    }
}