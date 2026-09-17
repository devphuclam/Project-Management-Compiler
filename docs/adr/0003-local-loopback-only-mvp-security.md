---
status: accepted
---

# Bind the MVP browser application to loopback only

MVP1 is a local internal analysis tool rather than a multi-user service. ASP.NET Core binds to loopback by default and does not expose the UI to the LAN. Authentication, SSO, remote deployment, and multi-tenant isolation remain outside MVP1; a later deployment decision must explicitly replace this security boundary before non-local access is enabled.
