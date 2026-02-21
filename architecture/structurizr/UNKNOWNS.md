# UNKNOWNS (to validate)

Use this file to capture what the code cannot tell you (yet). Keep it short and actionable.

## Ownership
- Who owns each service in production?
  - Order Accept API:
  - Order Process Worker:
  - Order Notification Service:
  - Frontend SPA:
  - Shared infra (AKS/APIM/Service Bus/Redis/SQL):

## Environments
- What environments exist (dev/test/staging/prod) and how do promotions work?
- Which subscriptions/resource groups map to which environments?

## Non-functional requirements (NFRs)
- Availability target:
- Latency target:
- DR (RPO/RTO):
- Security/compliance constraints:
- Cost constraints:

## Operations
- Where are dashboards/alerts?
- Where are runbooks?
- Incident process + escalation path?
