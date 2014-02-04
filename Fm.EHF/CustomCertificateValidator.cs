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
using System.ComponentModel;
using System.IdentityModel.Selectors;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using STARTLibrary.src.eu.peppol.start.security.handler;

namespace Fm.EHF
{
    /// <summary>
    /// Modified version of PEPPOLs custom validator
    /// </summary>
    public class CustomCertificateValidator : X509CertificateValidator 
    {
        private CertificateValidationThumbprints _thumbprints;
        private X509RevocationMode _revocationMode = X509RevocationMode.NoCheck; //Thomas endra til nocheck
        protected CertificateIssuer _expectedIssuer = CertificateIssuer.AccessPointCA;


        public CustomCertificateValidator()
        {
            _expectedIssuer = CertificateIssuer.AccessPointCA;
        }

        public override void Validate(X509Certificate2 certificate)
        {
            if (certificate == null)
                throw new SecurityTokenValidationException("Validation failed. No certificate to validate.");

            // 1) Validate that the certificate is either the PEPPOL root certificate or
            //    is issued by the correct CA 
            ValidateAsPEPPOLCertificate(certificate);

            // 2) Validate that the AP's certificate is the same as stated in the SMP
            ValidateAgainstExpectedCertificate(certificate);

            // 3) Possible custom validation
            ValidateSomeMore(certificate);
        }

        public virtual void ValidateSomeMore(X509Certificate2 certificate)
        {            
        }

        /// <summary>
        /// The expected issuer of the certificate being validated.
        /// </summary>
        /// <remarks>
        /// There are two intermediate CAs, one for AP certificates and another 
        /// one for SMP certificates.
        /// If this property is set, the validator does not only check that the
        /// certificate is valid, but also checks that the certificate of the
        /// intermediate CA which issued the certificate being validated is one 
        /// of the PEPPOL CAs.
        /// </remarks>
        public CertificateIssuer ExpectedIssuer
        {
            get { return _expectedIssuer; }
            set { _expectedIssuer = value; }
        }

        /// <summary>
        /// The thumbprints of certificates for the Root CA and the intermediate CAs.
        /// Required to identify the root certificate and (if ExpectedIssuer is set) 
        /// to verify the issuer of the certificate being validated.
        /// </summary>
        public CertificateValidationThumbprints Thumbprints
        {
            get
            {
                // If not set explicitly from the outside, try to use the default configuration:
                if (_thumbprints == null)
                {
                    _thumbprints = CertificateValidationThumbprints.FromConfiguration();
                }
                return _thumbprints;
            }
            set
            {
                _thumbprints = value;
            }

        }

        /// <summary>
        /// The expected certificate. 
        /// </summary>
        /// <remarks>
        /// Used to check that the certificate in a response matches the metadata
        /// received from the Service Metadata Publisher.
        /// If null, this check is skipped.
        /// </remarks>
        public X509Certificate2 ExpectedCertificate { get; set; }

        /// <summary>
        /// Specifies how the validator should check for X509 certificate revocation.
        /// Default is RevocationMode.Online.
        /// </summary>
        public X509RevocationMode RevocationMode
        {
            get { return _revocationMode; }
            set { _revocationMode = value; }
        }

        private void ValidateAsPEPPOLCertificate(X509Certificate2 certificate)
        {
            var chain = new X509Chain()
            {
                ChainPolicy = new X509ChainPolicy()
                {
                    RevocationMode = this.RevocationMode,
                    RevocationFlag = X509RevocationFlag.EndCertificateOnly
                }
            };

            //Validate chain errors or if it is revoked
            if (!BuildChain(chain, certificate))
            {
                throw new SecurityTokenValidationException("Validation failed. Chain could not be built.");
            }

            // Skip the check for the expected issuer if we are dealing with 
            // a certificate from the Root CA or an Intermediate CA:
            if (IsPeppolRoot(certificate) || IsPeppolIntermediateCA(certificate))
            {
                return;
            }

            if (!IsExpectedIssuer(chain))
            {
                throw new SecurityTokenValidationException("Validation failed. Issued by the wrong CA.");
            }
        }

        private bool BuildChain(X509Chain chain, X509Certificate2 certificate)
        {
            try
            {
                var buildOk = chain.Build(certificate);
                var chainOk = chain.HasNoError();

                if (buildOk && chainOk)
                {
                    return true;
                }
                return false;
            }
            catch (System.Security.Cryptography.CryptographicException exception)
            {
                return false;
            }
        }

        private bool IsExpectedIssuer(X509Chain chain)
        {
            if (_expectedIssuer == CertificateIssuer.None)
            {
                return true;
            }

            if (IssuerThumbprints == null)
            {
                throw new SecurityTokenValidationException("Validation failed. No intermediate CA certificate thumbprint(s) defined to check against.");
            }

            var issuer = chain.Issuer();

            return IssuerThumbprints.Contains(issuer.Certificate.Thumbprint, thumbprintComparer);
        }

        private bool IsPeppolRoot(X509Certificate2 certificate)
        {
            if (RootThumbprints == null)
            {
                throw new SecurityTokenValidationException("Validation failed. No Root CA certificate thumbprint(s) defined to check against.");
            }

            var chain = new X509Chain();
            chain.Build(certificate);

            for (var i = 1; i < chain.ChainElements.Count; i++)
            {
                if (RootThumbprints.Contains(chain.ChainElements[i].Certificate.Thumbprint, thumbprintComparer))
                    return true;
            }
            return false;
        }

        private bool IsPeppolIntermediateCA(X509Certificate2 certificate)
        {
            if (IssuerThumbprints == null)
            {
                return false;
            }

            var chain = new X509Chain();
            chain.Build(certificate);

            for (var i = 1; i < chain.ChainElements.Count; i++)
            {
                if (IssuerThumbprints.Contains(chain.ChainElements[i].Certificate.Thumbprint, thumbprintComparer))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// "When validating a signed response message, the sender Access Point SHOULD check 
        ///  that the certificate in the response matches the metadata received from the Service 
        ///  Metadata Publisher. This is done by comparing the subject common name in the 
        ///  certificate to the value stated in the metadata. This check ensures that only the 
        ///  legitimate Access Point stated in the service metadata will be able to produce 
        ///  correct responses."
        /// </summary>
        /// <param name="certificate">The certificate to be checked.</param>
        private void ValidateAgainstExpectedCertificate(X509Certificate2 certificate)
        {
            // The same validator might also be used for communication with a START client. In 
            // this case, no identifier is expected, and this validation step is skipped:
            if (ExpectedCertificate == null)
            {
                return;
            }

            if (!ExpectedCertificate.Equals(certificate))
            {
                throw new SecurityTokenValidationException("Validation failed. Certificate in the response does not match the metadata from the SMP.");
            }
        }

        #region CA thumbprints
        private static ThumbprintComparer thumbprintComparer = new ThumbprintComparer();
        private class ThumbprintComparer : System.Collections.Generic.IEqualityComparer<string>
        {
            public bool Equals(string x, string y)
            {
                return x.Equals(y, StringComparison.InvariantCultureIgnoreCase);
            }

            public int GetHashCode(string obj)
            {
                return obj.ToUpper().GetHashCode();
            }
        }

        private string[] RootThumbprints
        {
            get { return Thumbprints.RootCAThumbprints; }
        }

        private string[] IssuerThumbprints
        {
            get
            {
                switch (_expectedIssuer)
                {
                    case CertificateIssuer.None: return null;
                    case CertificateIssuer.AccessPointCA: return Thumbprints.AccessPointCAThumbprints;
                    case CertificateIssuer.SmpCA: return Thumbprints.SmpCAThumbprints;
                    default:
                        throw new InvalidEnumArgumentException("Value for ExpectedIssuer is not supported.");
                }
            }
        }
        #endregion
    }
}
