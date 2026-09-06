# ADR-0004: Publish the API image to GitHub Container Registry, not Azure Container Registry

Date: 2026-09-06. Status: accepted.

**Context.** The brief calls for containerised services on Azure. ACR Basic costs roughly £4 a month;
the hosting target is a free-tier App Service that can pull public images from any registry.

**Decision.** GitHub Actions builds the API image and pushes it to `ghcr.io/callan-jackson/switchpoint-api`
(public). The Bicep template points the Web App for Containers at that image and tag. Switching to
ACR is a parameter change.

**Consequences.** Zero registry cost; image is public (no secrets are baked in; configuration comes
from App Service settings and Key Vault).
