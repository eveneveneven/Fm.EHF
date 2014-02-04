===========================================================================
Fm.EHF
===========================================================================

DESCRIPTION
EHFUtility is a .NET-library to simplify EHF-related tasks in .NET. Use this utility to:
- Validate EHF-documents using DIFIs validationservice
- Send EHF-documents 
- Perform SMP and SML lookup

What you can’t do with this library:
- Receive PEPPOL-documents (Oxalis is a complete access point which can receive documents and send documents (java-lib or command-line))
- Generate EHF-xml. If you want to generate .NET-classes for EHF-Invoice: download XSD-schemas from DIFIs VEFaValidator-tool and use Xsd2Code or other tool

EHFUtility uses a modified version of PEPPOLs START-library (upgraded from .NET 3.5 to 4.5 with integrated support for claims) to send PEPPOL documents. Validation is performed using DIFIs validationservice.


INSTALLATION/CONFIGURATION:
1.	Add NuGet-package
2.	Install PEPPOL root certificates into trusted root certification authorities
3.	Create keystore with your private key and certificate issued by PEPPOL
4.	Modify app.config/web.config


RELEASE NOTES: