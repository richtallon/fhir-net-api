﻿using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Snapshot;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Validation;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Hl7.Fhir.Specification.Tests.Validation
{
    public class CustomResourceValidationTests
    {
        private static string _custom1 = "{\"resourceType\":\"CustomBasic\", \"id\": \"custom1\"}"; //, \"meta\":{\"profile\": [\"http://fire.ly/fhir/StructureDefinition/CustomBasic\"]}}";

        [Fact]
        public void CustomResourceCanBeValidated()
        {
            #region Read StructureDefinition for Custom Resource
            var structureDefJson = File.ReadAllText(@"TestData\CustomBasic-StructureDefinition-R3.json");
            var structureDefNode = FhirJsonNode.Parse(structureDefJson);
            var customBasicCanonical = structureDefNode.Children("url").First().Text;
            #endregion

            #region Create a Provider that knows this CustomBasic resource
            var structureDef = new FhirJsonParser().Parse<StructureDefinition>(structureDefJson);
            var snapShotGenerator = new SnapshotGenerator(ZipSource.CreateValidationSource());
            snapShotGenerator.Update(structureDef);

            var customResolver = new CustomResolver(new Dictionary<string, StructureDefinition> { { customBasicCanonical, structureDef } });
            bool mapTypeName(string typename, out string canonical) //It needs a custom typemapper to properly map CustomBasic to the full canonical url
            {
                if (typename == "CustomBasic")
                    canonical = customBasicCanonical;
                else
                    canonical = "http://hl7.org/fhir/StructureDefinition/" + typename;
                return true;
            }
            var provider = new StructureDefinitionSummaryProvider(customResolver, mapTypeName);
            #endregion

            #region Validate Custom Resource

            var customNode = FhirJsonNode.Parse(_custom1);
            var customTyped = customNode.ToTypedElement(provider);
            var typingErrors = customTyped.VisitAndCatch();
            Assert.Empty(typingErrors);

            var validator = new Validator(new ValidationSettings() { ResourceResolver = customResolver, GenerateSnapshot = true });
            var result = validator.Validate(customTyped);

            Assert.True(result.Success, "Validation should be successful but was not. Outcome: " + result.ToJson());
            #endregion
        }

        /// <summary>
        /// Resolves by default from the specification.zip, unless the canonical can be found in the _customSds dictionary.
        /// </summary>
        private class CustomResolver : IResourceResolver
        {
            private static IResourceResolver _coreResolver => ZipSource.CreateValidationSource();

            private readonly Dictionary<string, StructureDefinition> _customSds;

            public CustomResolver(Dictionary<string, StructureDefinition> customSds)
            {
                _customSds = customSds;
            }

            public Resource ResolveByUri(string uri)
            {
                if (_customSds.TryGetValue(uri, out var customSd))
                    return customSd;
                return _coreResolver.ResolveByUri(uri);
            }

            public Resource ResolveByCanonicalUri(string uri)
            {
                //You'll see a resolve call to "http://hl7.org/fhir/StructureDefinition/CustomBasic" where it should have been "http://fire.ly/fhir/StructureDefinition/CustomBasic".
                if (_customSds.TryGetValue(uri, out var customSd))
                    return customSd;

                return _coreResolver.ResolveByCanonicalUri(uri);
            }
        }
    }
}