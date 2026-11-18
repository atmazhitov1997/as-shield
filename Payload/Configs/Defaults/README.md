# Windows default security template

`Windows-Default-Security.inf` is a `secedit` export of the default local security policy of a clean Windows installation (built-in groups only, no machine-specific SIDs). The "Reset policies" action applies it with `secedit /configure` to bring account, audit and user-rights settings back to Windows defaults.
