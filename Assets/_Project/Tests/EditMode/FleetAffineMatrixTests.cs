using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DropletPrototype.Tests.EditMode
{
    public sealed class FleetAffineMatrixTests
    {
        [Test]
        public void RandomTrsCompositionsMatchUnityIncludingNonuniformAndNegativeScale()
        {
            var random = new System.Random(20260912);
            for (int i = 0; i < 1000; i++)
            {
                Matrix4x4 root = RandomTrs(random), local = RandomTrs(random);
                Matrix4x4 rootBefore = root, localBefore = local;
                AssertMatrix(root * local, FleetRenderManager.MultiplyAffine(in root, in local));
                AssertMatrix(rootBefore, root);
                AssertMatrix(localBefore, local);
            }
        }

        [Test]
        public void NestedShearedAffineTransformsKeepTranslationAndEveryBasisComponent()
        {
            var random = new System.Random(73191);
            for (int i = 0; i < 250; i++)
            {
                // Rotations between nonuniform scales create shear. Restricting
                // this optimization to a decomposed TRS would change the model.
                Matrix4x4 root = RandomTrs(random) * RandomTrs(random);
                Matrix4x4 local = RandomTrs(random) * RandomTrs(random);
                AssertMatrix(root * local, FleetRenderManager.MultiplyAffine(in root, in local));
                AssertMatrix(root, FleetRenderManager.MultiplyAffine(in root, Matrix4x4.identity));
                AssertMatrix(local, FleetRenderManager.MultiplyAffine(Matrix4x4.identity, in local));
            }
        }

        [TestCase(1, 1)]
        [TestCase(511, 1)]
        [TestCase(133, 4)]
        [TestCase(600, 5)]
        public void FixedArrayTraversalPreservesShipPartOrderAnd511Remainders(int ships, int parts)
        {
            const int capacity = 511;
            var random = new System.Random(510511);
            var roots = new Matrix4x4[ships];
            var locals = new Matrix4x4[parts];
            for (int i = 0; i < roots.Length; i++) roots[i] = RandomTrs(random);
            for (int i = 0; i < locals.Length; i++) locals[i] = RandomTrs(random);
            var baseline = new List<Matrix4x4>(ships * parts);
            foreach (Matrix4x4 root in roots)
                foreach (Matrix4x4 local in locals) baseline.Add(root * local);

            var submitted = new List<Matrix4x4>(baseline.Count);
            var batch = new Matrix4x4[capacity];
            var batchSizes = new List<int>();
            int count = 0;
            for (int ship = 0; ship < roots.Length; ship++)
            for (int part = 0; part < locals.Length; part++)
            {
                batch[count++] = FleetRenderManager.MultiplyAffine(in roots[ship], in locals[part]);
                if (count < capacity) continue;
                CopyBatch(batch, count, submitted); batchSizes.Add(count); count = 0;
            }
            if (count > 0) { CopyBatch(batch, count, submitted); batchSizes.Add(count); }
            Assert.AreEqual(baseline.Count, submitted.Count);
            Assert.AreEqual(Mathf.CeilToInt(baseline.Count / (float)capacity), batchSizes.Count);
            for (int i = 0; i < submitted.Count; i++) AssertMatrix(baseline[i], submitted[i]);
            for (int i = 0; i < batchSizes.Count - 1; i++) Assert.AreEqual(capacity, batchSizes[i]);
            Assert.AreEqual((baseline.Count - 1) % capacity + 1, batchSizes[batchSizes.Count - 1]);
        }

        static void CopyBatch(Matrix4x4[] batch, int count, List<Matrix4x4> submitted)
        { for (int i = 0; i < count; i++) submitted.Add(batch[i]); }

        static Matrix4x4 RandomTrs(System.Random random)
        {
            return Matrix4x4.TRS(new Vector3(Value(random, 30000), Value(random, 30000), Value(random, 30000)),
                Quaternion.Euler(Value(random, 180), Value(random, 180), Value(random, 180)),
                new Vector3(Scale(random), Scale(random), Scale(random)));
        }
        static float Value(System.Random random, float magnitude) => (float)(random.NextDouble() * 2 - 1) * magnitude;
        static float Scale(System.Random random) => (.1f + (float)random.NextDouble() * 7) * (random.Next(2) == 0 ? -1 : 1);
        static void AssertMatrix(Matrix4x4 expected, Matrix4x4 actual)
        {
            for (int element = 0; element < 16; element++)
                Assert.That(actual[element], Is.EqualTo(expected[element]).Within(Mathf.Max(.00001f, Mathf.Abs(expected[element]) * .0000002f)),
                    "Matrix element " + element);
        }
    }
}
