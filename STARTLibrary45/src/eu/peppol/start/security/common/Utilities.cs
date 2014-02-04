/*Version: MPL 1.1/EUPL 1.1
 * 
 * The contents of this file are subject to the Mozilla Public License Version 
 * 1.1 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at:
 * http://www.mozilla.org/MPL/
 * 
 * Software distributed under the License is distributed on an "AS IS" basis,
 * WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License
 * for the specific language governing rights and limitations under the
 * License.
 * 
 * The Original Code is Copyright The PEPPOL project (http://www.peppol.eu)
 * 
 * Alternatively, the contents of this file may be used under the
 * terms of the EUPL, Version 1.1 or - as soon they will be approved 
 * by the European Commission - subsequent versions of the EUPL 
 * (the "Licence"); You may not use this work except in compliance 
 * with the Licence.
 * You may obtain a copy of the Licence at:
 * http://www.osor.eu/eupl/european-union-public-licence-eupl-v.1.1
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the Licence is distributed on an "AS IS" basis,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the Licence for the specific language governing permissions and
 * limitations under the Licence.
 * 
 * If you wish to allow use of your version of this file only
 * under the terms of the EUPL License and not to allow others to use
 * your version of this file under the MPL, indicate your decision by
 * deleting the provisions above and replace them with the notice and
 * other provisions required by the EUPL License. If you do not delete
 * thev provisions above, a recipient may use your version of this file
 * under either the MPL or the EUPL License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel.Channels;
using System.ServiceModel.Security.Tokens;
using System.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Xml;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Net.Security;
using STARTLibrary.src.eu.peppol.start.security.handler;
using STARTLibrary.accesspointService;
using STARTLibrary.src.eu.peppol.start.io;
using STARTLibrary.src.eu.peppol.start.impl;
using STARTLibrary.src.eu.peppol.start.common;
using System.Diagnostics;

namespace STARTLibrary.src.eu.peppol.start.security.common
{
    public class Utilities
    {
        public static AsymmetricSecurityBindingElement ConfigBinding(Binding b)
        {
            var c = b as CustomBinding;
            if (c == null) return null;

            b.Namespace = "http://www.w3.org/2009/02/ws-tra";

            var abe = c.Elements.Find<AsymmetricSecurityBindingElement>();
            if (abe == null) return null;

            abe.EndpointSupportingTokenParameters.SetKeyDerivation(false);

            var dd = new IssuedSecurityTokenParameters("http://docs.oasis-open.org/wss/oasis-wss-saml-token-profile-1.1#SAMLV2.0")
            {
                KeyType = SecurityKeyType.AsymmetricKey,
                RequireDerivedKeys = abe.InitiatorTokenParameters.RequireDerivedKeys,
                InclusionMode = abe.InitiatorTokenParameters.InclusionMode,
                ReferenceStyle = SecurityTokenReferenceStyle.Internal,
                UseStrTransform = true
            };

            abe.EndpointSupportingTokenParameters.Signed.Add(dd);

            return abe;
        }

        public static ChannelFactory<T> CreateResourceChannelFactory<T>(string endpointName, X509Certificate2 clientCertificate, X509Certificate2 serviceCertificate)
        {
            return CreateResourceChannelFactory<T>(endpointName, (cf, c) =>
            {
                c.ClientCertificate.Certificate = clientCertificate;
                c.ServiceCertificate.DefaultCertificate = serviceCertificate;
                c.UseIdentityConfiguration = true; //added by thomas (.net 4.5)
            });
        }

        public static ChannelFactory<T> CreateResourceChannelFactory<T>(string endpointName, Action<ChannelFactory<T>, ClientCredentials> ac)
        {
            var resourceFactory = new ChannelFactory<T>(endpointName);            
            ConfigBinding(resourceFactory.Endpoint.Binding);
            ac(resourceFactory, resourceFactory.Credentials);
            resourceFactory.Endpoint.Contract.ProtectionLevel = ProtectionLevel.Sign;

            //NOTE: Thomas removed (ugrade to .-NET 4.5):
            //resourceFactory.ConfigureChannelFactory();
            //http://social.msdn.microsoft.com/Forums/vstudio/en-US/9cffe4ea-5a0d-4729-9eb1-b56629b175dc/configurechannelfactory-in-net-45?forum=Geneva            
            //http://bartwullems.blogspot.no/2013/08/wcf-enable-wif-security-integration.html
            return resourceFactory;
        }

        private static void SetExpectedServiceCertificate(ChannelFactory<STARTLibrary.accesspointService.Resource> factory, X509Certificate2 expectedCertificate)
        {
            SetExpectedServiceCertificate(factory.Credentials.ServiceCertificate, expectedCertificate);
            SetIdentity(factory.Endpoint, expectedCertificate);
        }

        private static void SetExpectedServiceCertificate(
            System.ServiceModel.Security.X509CertificateRecipientClientCredential serviceCertificate, X509Certificate2 expectedCertificate)
        {
            const X509RevocationMode revocationMode = X509RevocationMode.NoCheck; // Set to Online if revocation check is required 

            serviceCertificate.DefaultCertificate = expectedCertificate;

            // To validate that the certificate used to sign a response message matches the 
            // information published in the Service Metadata Publishing, the validator needs 
            // to know this certificate:
            serviceCertificate.Authentication.CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.Custom;
            serviceCertificate.Authentication.RevocationMode = revocationMode;
            serviceCertificate.Authentication.CustomCertificateValidator = new CertificateValidator()
            {
                ExpectedCertificate = expectedCertificate,
                ExpectedIssuer = CertificateIssuer.AccessPointCA,
                RevocationMode = revocationMode
            };
        }

        private static void SetIdentity(ServiceEndpoint serviceEndpoint, X509Certificate2 expectedCertificate)
        {
            var dnsName = expectedCertificate.GetNameInfo(X509NameType.SimpleName, false);

            serviceEndpoint.Address = new EndpointAddress(
                serviceEndpoint.Address.Uri,
                EndpointIdentity.CreateDnsIdentity(dnsName),
                serviceEndpoint.Address.Headers);
        }

        public static string CallCreate(ChannelFactory<Resource> resourceFactory, Uri thisUrl, string smlDomain, int assuranceLevel, CreateRequest request, IMessageMetadata messageMetadata)
        {
            STARTLibrary.accesspointService.Resource ws = null;

            try
            {
                var serviceMetadata = !string.IsNullOrEmpty(smlDomain) ?
                                           ServiceMetadata.FromSml(smlDomain, request) :
                                           ServiceMetadata.FromKnownEndpoint(resourceFactory);

                //NOTE: if you want to debug and send to "wrong" address, change below:
                //var test1 = ServiceMetadata.FromSml(smlDomain, request); //henter metadata fra SML
                //var test2 = ServiceMetadata.FromKnownEndpoint(resourceFactory);
                //var test3 = new ServiceMetadata()
                //    {
                //        Address = new Uri("https://ehf.fylkesmannen.no/oxalis"), //factory.Endpoint.Address.Uri,
                //        Certificate = resourceFactory.Credentials.ServiceCertificate.DefaultCertificate
                //    };
                //serviceMetadata = test1;

                if (serviceMetadata.Certificate == null)
                {
                    throw new ArgumentException("Certificate required (both for custom validation and for authenticationMode \"MutualCertificate\").");
                }

                SetExpectedServiceCertificate(resourceFactory, serviceMetadata.Certificate);

                X509Certificate2 clientCertificate = resourceFactory.Credentials.ClientCertificate.Certificate;

                Saml2SecurityToken token = AccessPointToken.ClaimsToSaml2SenderVouchesToken(
                    AccessPointToken.MakeClaims(request.SenderIdentifier.Value, assuranceLevel),
                    thisUrl.AbsoluteUri, clientCertificate);

                //NOTE: changed when upgrading to .NET 4.5:
                //ws = resourceFactory.CreateChannelWithIssuedToken(new EndpointAddress(thisUrl, resourceFactory.Endpoint.Address.Identity), token);
                ws = resourceFactory.CreateChannelWithIssuedToken(token, new EndpointAddress(thisUrl, resourceFactory.Endpoint.Address.Identity));

                var response = ws.Create(request);
                return "Document sent.";
            }
            catch (Exception exception)
            {
                throw exception;
            }
            finally
            {
                if (ws is IDisposable)
                {
                    (ws as IDisposable).Dispose();
                }
            };
        }
    }
}
