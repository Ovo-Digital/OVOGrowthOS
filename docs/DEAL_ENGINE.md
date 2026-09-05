# Deal engine

Deals are generated only from approved evaluations. Every option stores the exact deal type and parameters, contract term, baseline amount/period/method, setup investment, estimated internal cost, conditions, and evaluation/rule/financial/commission snapshots.

Side-by-side comparison uses deterministic projected economics and scores OVO margin, brand contribution margin, setup payback, and risk. It returns one recommended option with reasons; accepting remains an explicit user action. Activation requires `Accepted` status and moves the linked brand to `Active`.

Lifecycle: `Draft → InternalReview → Proposed → Negotiation → Accepted/Rejected → Active → Expired/Terminated`. The current UI covers draft option generation, comparison, explicit acceptance, and activation; later legal negotiation states remain API/domain-ready.
