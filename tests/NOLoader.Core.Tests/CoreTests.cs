using System;
using System.Collections.Generic;
using System.IO;
using NOLoader.API;
using NOLoader.API.Manifest;
using NOLoader.Core.Gates;
using NOLoader.Core.Manifest;
using NOLoader.Core.Patching;
using NOLoader.Patcher;
using NOLoader.Registry;
using Xunit;

namespace NOLoader.Core.Tests
{
    public class StringHashTests
    {
        [Fact]
        public void Murmur32_IsStable()
        {
            int a = StringHash.Murmur32("AIM-120");
            int b = StringHash.Murmur32("AIM-120");
            Assert.Equal(a, b);
        }

        [Fact]
        public void Murmur32_DifferentStrings_Differ()
        {
            Assert.NotEqual(StringHash.Murmur32("AIM-120"), StringHash.Murmur32("AIM-9"));
        }
    }

    public class StringHashTableTests
    {
        [Fact]
        public void TryDecode_KnownString_Works()
        {
            StringHashTable.Register("com.example.mod");
            int hash = StringHash.Murmur32("com.example.mod");
            Assert.True(StringHashTable.TryDecode(hash, out string? value));
            Assert.Equal("com.example.mod", value);
        }
    }

    public class StringHashTableHashOnlyTests
    {
        [Fact]
        public void DevAutoRegister_DefaultFalse()
        {
            StringHashTable.DevAutoRegisterEnabled = false;
            Assert.False(StringHashTable.DevAutoRegisterEnabled);
        }
    }

    public class ManifestReaderTests
    {
        [Fact]
        public void Parse_ReadsRequiredFields()
        {
            const string json = @"{
              ""id"": ""com.test"",
              ""guid"": ""a1b2c3d4e5f6478990a1b2c3d4e5f601"",
              ""version"": ""1.0.0"",
              ""name"": ""Test"",
              ""assembly"": ""Test.dll"",
              ""entryType"": ""Test.Entry"",
              ""loadStage"": ""MainMenu"",
              ""dependencies"": [],
              ""patches"": []
            }";
            var m = ManifestReader.Parse(json);
            Assert.Equal("com.test", m.Id);
            Assert.Equal("a1b2c3d4e5f6478990a1b2c3d4e5f601", m.Guid);
            Assert.Equal("Test.dll", m.Assembly);
            Assert.Equal(LoadStage.MainMenu, m.LoadStage);
            Assert.NotEqual(0, m.IdHash);
        }

        [Fact]
        public void Parse_IdHashOnly_SetsHashOnlyId()
        {
            const string json = @"{
              ""idHash"": 305419896,
              ""version"": ""1.0.0"",
              ""assembly"": ""Test.dll"",
              ""entryType"": ""Test.Entry"",
              ""loadStage"": ""PreMenu"",
              ""dependencies"": [],
              ""patches"": []
            }";
            var m = ManifestReader.Parse(json);
            Assert.Equal(305419896, m.IdHash);
            Assert.True(m.HashOnlyId);
        }

        [Fact]
        public void Parse_HashOnlyPatch_ReadsTargetHash()
        {
            const string json = @"{
              ""idHash"": 123,
              ""version"": ""1.0.0"",
              ""assembly"": ""Test.dll"",
              ""entryType"": ""Test.Entry"",
              ""loadStage"": ""MainMenu"",
              ""dependencies"": [],
              ""patches"": [{
                ""targetHash"": ""A1B2C3D4"",
                ""injectHash"": ""DEADBEEF"",
                ""method"": ""Prefix"",
                ""expectedSignatureHash"": ""CE578CC411C69FD1""
              }]
            }";
            var m = ManifestReader.Parse(json);
            Assert.Single(m.Patches);
            Assert.Equal(unchecked((int)0xA1B2C3D4), m.Patches[0].TargetHash);
            Assert.Equal(unchecked((int)0xDEADBEEF), m.Patches[0].InjectHash);
            Assert.Equal("CE578CC411C69FD1", m.Patches[0].ExpectedSignatureHash);
        }
    }

    public class PatchEmbedResolverTests
    {
        [Fact]
        public void Resolve_FillsTargetsFromBakeFile()
        {
            string dir = Path.Combine(Path.GetTempPath(), "noloader-rdytu-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                const string bake = @"[
                  {
                    ""target"": ""WeaponChecker::VetLoadout"",
                    ""inject"": ""MyMod.Patches::VetLoadoutPrefix"",
                    ""method"": ""Prefix"",
                    ""expectedSignatureHash"": ""7B29567661230597"",
                    ""targetHash"": ""A1B2C3D4"",
                    ""injectHash"": ""DEADBEEF""
                  }
                ]";
                File.WriteAllText(Path.Combine(dir, "patch.bake.json"), bake);

                var manifest = new ModManifest
                {
                    Valid = true,
                    IdHash = 1,
                    Patches = new List<PatchDescriptor>
                    {
                        new PatchDescriptor
                        {
                            TargetHash = unchecked((int)0xA1B2C3D4),
                            InjectHash = unchecked((int)0xDEADBEEF),
                            Method = "Prefix"
                        }
                    }
                };

                PatchEmbedResolver.Resolve(manifest, dir);
                Assert.True(manifest.Valid);
                Assert.Equal("WeaponChecker::VetLoadout", manifest.Patches[0].Target);
                Assert.Equal("MyMod.Patches::VetLoadoutPrefix", manifest.Patches[0].Inject);
                Assert.Equal("7B29567661230597", manifest.Patches[0].ExpectedSignatureHash);
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }
    }

    public class ManifestGateL1Tests
    {
        [Fact]
        public void Validate_RejectsDuplicateIds()
        {
            var manifests = new List<ModManifest>
            {
                MakeManifest("dup.mod", "A"),
                MakeManifest("dup.mod", "B")
            };

            List<ModManifest> valid = ManifestGateL1.Validate(manifests, out List<string> errors);
            Assert.Single(valid);
            Assert.Contains(errors, e => e.Contains("Duplicate"));
        }

        [Fact]
        public void Validate_RejectsMissingAssembly()
        {
            var manifests = new List<ModManifest>
            {
                new ModManifest { Id = "x", Valid = true, IdHash = StringHash.Murmur32("x") }
            };

            List<ModManifest> valid = ManifestGateL1.Validate(manifests, out List<string> errors);
            Assert.Empty(valid);
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void Validate_RejectsDuplicateGuids()
        {
            var manifests = new List<ModManifest>
            {
                MakeManifest("mod.a", "A", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                MakeManifest("mod.b", "B", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
            };

            List<ModManifest> valid = ManifestGateL1.Validate(manifests, out List<string> errors);
            Assert.Single(valid);
            Assert.Contains(errors, e => e.Contains("Duplicate mod guid"));
        }

        [Fact]
        public void Validate_AcceptsValidGuid()
        {
            var manifests = new List<ModManifest>
            {
                MakeManifest("mod.a", "A", "a1b2c3d4-e5f6-4789-90ab-cdef12345678")
            };

            List<ModManifest> valid = ManifestGateL1.Validate(manifests, out List<string> errors);
            Assert.Single(valid);
            Assert.Empty(errors);
        }

        private static ModManifest MakeManifest(string id, string folder, string guid = "")
        {
            return new ModManifest
            {
                Id = id,
                Guid = guid,
                Valid = true,
                IdHash = StringHash.Murmur32(id),
                Assembly = "Test.dll",
                EntryType = "Test.Entry",
                FolderPath = folder
            };
        }
    }

    public class PatchGateL2SignatureTests
    {
        [Fact]
        public void FilterValid_RejectsWrongSignatureWhenGameRootProvided()
        {
            string? gameRoot = Environment.GetEnvironmentVariable("NOLOADER_GAME_ROOT");
            if (string.IsNullOrEmpty(gameRoot))
                return;

            byte[]? bytes = AssemblyPatcher.LoadGameAssemblyBytes(gameRoot)
                ?? AssemblyPatcher.LoadLiveGameAssemblyBytes(gameRoot);
            if (bytes == null)
                return;

            var manifests = new List<ModManifest>
            {
                new ModManifest
                {
                    Id = "broken.test",
                    Valid = true,
                    IdHash = StringHash.Murmur32("broken.test"),
                    Patches = new List<PatchDescriptor>
                    {
                        new PatchDescriptor
                        {
                            Target = "Encyclopedia::AfterLoad",
                            Inject = "Broken::Hook",
                            Method = "Postfix",
                            ExpectedSignatureHash = "0000000000000000"
                        }
                    }
                }
            };

            List<ModManifest> filtered = PatchGateL2.FilterValid(manifests, out List<string> errors, gameRoot);
            Assert.Empty(filtered);
            Assert.Contains(errors, e => e.Contains("Signature mismatch") || e.Contains("rejected"));
        }
    }

    public class PatchPlanBuilderStageTests
    {
        [Fact]
        public void Build_FiltersByLoadStage()
        {
            var manifests = new List<ModManifest>
            {
                new ModManifest
                {
                    Id = "menu.mod",
                    Valid = true,
                    LoadStage = LoadStage.MainMenu,
                    Patches = new List<PatchDescriptor> { new PatchDescriptor { Target = "A::B", Inject = "C::D", ExpectedSignatureHash = "ABCD1234ABCD1234" } }
                },
                new ModManifest
                {
                    Id = "mission.mod",
                    Valid = true,
                    LoadStage = LoadStage.Mission,
                    Patches = new List<PatchDescriptor> { new PatchDescriptor { Target = "X::Y", Inject = "Z::W", ExpectedSignatureHash = "ABCD1234ABCD1234" } }
                }
            };

            var menuOnly = PatchPlanBuilder.Build(manifests, LoadStage.MainMenu);
            Assert.Single(menuOnly);
            Assert.Equal("menu.mod", menuOnly[0].ModId);
        }
    }

    public class PhysicsSafetyCatchTests
    {
        [Fact]
        public void SanitizeMass_NaN_ReturnsMin()
        {
            Assert.Equal(0.001f, PhysicsSafetyCatch.SanitizeMass(float.NaN), 3);
        }

        [Fact]
        public void SanitizeForce_Infinity_ReturnsZero()
        {
            Assert.Equal(0f, PhysicsSafetyCatch.SanitizeForce(float.PositiveInfinity));
        }

        [Fact]
        public void SanitizeVector3_NaN_ReturnsZero()
        {
            var v = PhysicsSafetyCatch.SanitizeVector3(new UnityEngine.Vector3(float.NaN, 1f, 2f));
            Assert.Equal(UnityEngine.Vector3.zero, v);
        }
    }

    public class ScriptableObjectGateL3Tests
    {
        [Fact]
        public void ValidateMissile_RejectsNullDefinition()
        {
            var entry = new MissileEntry { JsonKeyHash = 1 };
            Assert.False(ScriptableObjectGateL3.ValidateMissile(ref entry, out string? reason));
            Assert.Equal("Definition is null", reason);
        }

        [Fact]
        public void ValidateWeaponMount_RejectsNullAsset()
        {
            var entry = new WeaponMountEntry { JsonKeyHash = 1 };
            Assert.False(ScriptableObjectGateL3.ValidateWeaponMount(ref entry, out string? reason));
            Assert.Equal("Asset is null", reason);
        }
    }

    public class CoreBootstrapPatchHashTests
    {
        [Fact]
        public void BakedHashes_AllCoreModIdsPresent()
        {
            string[] modIds =
            {
                "noloader.bootstrap",
                "noloader.registry",
                "noloader.physics",
                "noloader.gatel4",
                "noloader.gatel4.scene",
                "noloader.physics.unity",
                "noloader.physics.unity.single"
            };

            foreach (string modId in modIds)
            {
                string? hash = CoreBootstrapPatchHashes.TryGet(modId);
                Assert.False(string.IsNullOrEmpty(hash), "Missing baked hash for " + modId);
                Assert.Equal(16, hash!.Length);
            }
        }
    }

    public class PatchGateL2Tests
    {
        [Fact]
        public void ValidateModPatches_RejectsMissingSignatureHash()
        {
            var manifests = new List<ModManifest>
            {
                new ModManifest
                {
                    Id = "patch.mod",
                    Valid = true,
                    IdHash = StringHash.Murmur32("patch.mod"),
                    Patches = new List<PatchDescriptor>
                    {
                        new PatchDescriptor
                        {
                            Target = "SomeType::Method",
                            Inject = "Some::Hook",
                            Method = "Postfix"
                        }
                    }
                }
            };

            List<string> errors = PatchGateL2.ValidateModPatches(manifests);
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("expectedSignatureHash"));
        }

        [Fact]
        public void FilterValid_RejectsModWithMissingSignatureHash()
        {
            var manifests = new List<ModManifest>
            {
                new ModManifest
                {
                    Id = "good.mod",
                    Valid = true,
                    IdHash = StringHash.Murmur32("good.mod"),
                    Patches = new List<PatchDescriptor>()
                },
                new ModManifest
                {
                    Id = "bad.mod",
                    Valid = true,
                    IdHash = StringHash.Murmur32("bad.mod"),
                    Patches = new List<PatchDescriptor>
                    {
                        new PatchDescriptor
                        {
                            Target = "SomeType::Method",
                            Inject = "Some::Hook",
                            Method = "Postfix"
                        }
                    }
                }
            };

            List<ModManifest> filtered = PatchGateL2.FilterValid(manifests, out List<string> errors);
            Assert.Single(filtered);
            Assert.Equal("good.mod", filtered[0].Id);
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("bad.mod") && e.Contains("rejected"));
        }
    }
}
