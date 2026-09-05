# Rules engine

Rules are database rows with category, typed field, operator, primary/secondary values, severity, weight, enabled state, and recommendation effect. Supported operators are equality/inequality, greater/less variants, and inclusive `Between`.

Rulesets use `Draft`, `Published`, and `Archived` states. Only drafts are editable. Publishing archives the previous published set and updates the default ruleset setting. To change production policy, clone the published version, edit the new draft, review it, and explicitly publish it.

Evaluation analysis loads the configured published version, runs typed rules and composite guards, and freezes the complete rule payload with its identifier/version. Historical evaluations never re-evaluate merely because a newer policy is published.
