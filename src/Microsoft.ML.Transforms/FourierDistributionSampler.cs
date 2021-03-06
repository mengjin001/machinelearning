// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.ML;
using Microsoft.ML.CommandLine;
using Microsoft.ML.EntryPoints;
using Microsoft.ML.Internal.Utilities;
using Microsoft.ML.Model;
using Microsoft.ML.Transforms;

[assembly: LoadableClass(typeof(GaussianFourierSampler), typeof(GaussianFourierSampler.Options), typeof(SignatureFourierDistributionSampler),
    "Gaussian Kernel", GaussianFourierSampler.LoadName, "Gaussian")]

[assembly: LoadableClass(typeof(LaplacianFourierSampler), typeof(LaplacianFourierSampler.Options), typeof(SignatureFourierDistributionSampler),
    "Laplacian Kernel", LaplacianFourierSampler.RegistrationName, "Laplacian")]

// This is for deserialization from a binary model file.
[assembly: LoadableClass(typeof(GaussianFourierSampler), null, typeof(SignatureLoadModel),
    "Gaussian Fourier Sampler Executor", "GaussianSamplerExecutor", GaussianFourierSampler.LoaderSignature)]

// This is for deserialization from a binary model file.
[assembly: LoadableClass(typeof(LaplacianFourierSampler), null, typeof(SignatureLoadModel),
    "Laplacian Fourier Sampler Executor", "LaplacianSamplerExecutor", LaplacianFourierSampler.LoaderSignature)]

// REVIEW: Roll all of this in with the RffTransform.
namespace Microsoft.ML.Transforms
{
    /// <summary>
    /// Signature for an IFourierDistributionSampler constructor.
    /// </summary>
    [BestFriend]
    internal delegate void SignatureFourierDistributionSampler(float avgDist);

    public interface IFourierDistributionSampler : ICanSaveModel
    {
        float Next(Random rand);
    }

    [TlcModule.ComponentKind("FourierDistributionSampler")]
    internal interface IFourierDistributionSamplerFactory : IComponentFactory<float, IFourierDistributionSampler>
    {
    }

    public sealed class GaussianFourierSampler : IFourierDistributionSampler
    {
        private readonly IHost _host;

        public sealed class Options : IFourierDistributionSamplerFactory
        {
            [Argument(ArgumentType.AtMostOnce, HelpText = "gamma in the kernel definition: exp(-gamma*||x-y||^2 / r^2). r is an estimate of the average intra-example distance", ShortName = "g")]
            public float Gamma = 1;

            IFourierDistributionSampler IComponentFactory<float, IFourierDistributionSampler>.CreateComponent(IHostEnvironment env, float avgDist)
                => new GaussianFourierSampler(env, this, avgDist);
        }

        internal const string LoaderSignature = "RandGaussFourierExec";
        private static VersionInfo GetVersionInfo()
        {
            return new VersionInfo(
                modelSignature: "RND GAUS",
                verWrittenCur: 0x00010001, // Initial
                verReadableCur: 0x00010001,
                verWeCanReadBack: 0x00010001,
                loaderSignature: LoaderSignature,
                loaderAssemblyName: typeof(GaussianFourierSampler).Assembly.FullName);
        }

        internal const string LoadName = "GaussianRandom";

        private readonly float _gamma;

        public GaussianFourierSampler(IHostEnvironment env, Options options, float avgDist)
        {
            Contracts.CheckValue(env, nameof(env));
            _host = env.Register(LoadName);
            _host.CheckValue(options, nameof(options));

            _gamma = options.Gamma / avgDist;
        }

        private static GaussianFourierSampler Create(IHostEnvironment env, ModelLoadContext ctx)
        {
            Contracts.CheckValue(env, nameof(env));
            env.CheckValue(ctx, nameof(ctx));
            ctx.CheckAtModel(GetVersionInfo());
            return new GaussianFourierSampler(env, ctx);
        }

        private GaussianFourierSampler(IHostEnvironment env, ModelLoadContext ctx)
        {
            Contracts.AssertValue(env);
            _host = env.Register(LoadName);
            _host.AssertValue(ctx);

            // *** Binary format ***
            // int: sizeof(Float)
            // Float: gamma

            int cbFloat = ctx.Reader.ReadInt32();
            _host.CheckDecode(cbFloat == sizeof(float));

            _gamma = ctx.Reader.ReadFloat();
            _host.CheckDecode(FloatUtils.IsFinite(_gamma));
        }

        void ICanSaveModel.Save(ModelSaveContext ctx)
        {
            ctx.SetVersionInfo(GetVersionInfo());

            // *** Binary format ***
            // int: sizeof(Float)
            // Float: gamma

            ctx.Writer.Write(sizeof(float));
            _host.Assert(FloatUtils.IsFinite(_gamma));
            ctx.Writer.Write(_gamma);
        }

        public float Next(Random rand)
        {
            return (float)Stats.SampleFromGaussian(rand) * MathUtils.Sqrt(2 * _gamma);
        }
    }

    public sealed class LaplacianFourierSampler : IFourierDistributionSampler
    {
        public sealed class Options : IFourierDistributionSamplerFactory
        {
            [Argument(ArgumentType.AtMostOnce, HelpText = "a in the term exp(-a|x| / r). r is an estimate of the average intra-example L1 distance")]
            public float A = 1;

            IFourierDistributionSampler IComponentFactory<float, IFourierDistributionSampler>.CreateComponent(IHostEnvironment env, float avgDist)
                => new LaplacianFourierSampler(env, this, avgDist);
        }

        private static VersionInfo GetVersionInfo()
        {
            return new VersionInfo(
                modelSignature: "RND LPLC",
                verWrittenCur: 0x00010001, // Initial
                verReadableCur: 0x00010001,
                verWeCanReadBack: 0x00010001,
                loaderSignature: LoaderSignature,
                loaderAssemblyName: typeof(LaplacianFourierSampler).Assembly.FullName);
        }

        internal const string LoaderSignature = "RandLaplacianFourierExec";
        internal const string RegistrationName = "LaplacianRandom";

        private readonly IHost _host;
        private readonly float _a;

        public LaplacianFourierSampler(IHostEnvironment env, Options options, float avgDist)
        {
            Contracts.CheckValue(env, nameof(env));
            _host = env.Register(RegistrationName);
            _host.CheckValue(options, nameof(options));

            _a = options.A / avgDist;
        }

        private static LaplacianFourierSampler Create(IHostEnvironment env, ModelLoadContext ctx)
        {
            Contracts.CheckValue(env, nameof(env));
            env.CheckValue(ctx, nameof(ctx));
            ctx.CheckAtModel(GetVersionInfo());

            return new LaplacianFourierSampler(env, ctx);
        }

        private LaplacianFourierSampler(IHostEnvironment env, ModelLoadContext ctx)
        {
            Contracts.AssertValue(env);
            _host = env.Register(RegistrationName);
            _host.AssertValue(ctx);

            // *** Binary format ***
            // int: sizeof(Float)
            // Float: a

            int cbFloat = ctx.Reader.ReadInt32();
            _host.CheckDecode(cbFloat == sizeof(float));

            _a = ctx.Reader.ReadFloat();
            _host.CheckDecode(FloatUtils.IsFinite(_a));
        }

        void ICanSaveModel.Save(ModelSaveContext ctx)
        {
            ctx.SetVersionInfo(GetVersionInfo());

            // *** Binary format ***
            // int: sizeof(Float)
            // Float: a

            ctx.Writer.Write(sizeof(float));
            _host.Assert(FloatUtils.IsFinite(_a));
            ctx.Writer.Write(_a);
        }

        public float Next(Random rand)
        {
            return _a * Stats.SampleFromCauchy(rand);
        }
    }
}
