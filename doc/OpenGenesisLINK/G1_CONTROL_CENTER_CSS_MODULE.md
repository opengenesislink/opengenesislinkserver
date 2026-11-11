# OGL World API Control Center: CSS module separation

The protected page previously embedded 48,161 bytes of CSS into
`NexApiDocsPage.cs` alongside HTML and JavaScript.

The exact CSS payload is now provided by `NexApiDocsStyles.Content`,
inserted into the original inline `<style>` block before serving the
protected HTML. No new route, asset fetch, CSP allowance, localStorage,
authentication, or caching behavior is introduced.

The page keeps its markup and JavaScript inline to preserve the
existing static CI contracts and the signed admin-session handoff.
`verify_ogl_api_adminlte_viewers.py` reconstructs the effective
markup for static style and Node.js syntax checks.

This is the first non-disruptive UI module extraction. A follow-up
JavaScript extraction must update every source-inspecting CI gate
before deleting the old inline script. Never duplicate the runtime
handlers or weaken `/api/v1/docs` authentication during extraction.
