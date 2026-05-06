# Lab 7 — Customizing Authentication Flows

This lab addresses the problem that Keycloak's default authentication experience may not match an organization's specific security requirements or user experience goals. By the end, you will have demonstrated how to customize the browser authentication flow without writing code so that login behavior matches the realm's requirements.

---

## Prerequisites

Before starting this lab, confirm:

- [ ] Keycloak is running at `http://localhost:8080` (or the shared cloud instance URL provided by the instructor)
- [ ] The `{realm}` realm is accessible with at least one test user
- [ ] Lab 7 is completed (realm and users configured)

If any prerequisite is missing, complete [Lab 7] before continuing.

---

## Background

### Authentication flows

Keycloak allows you to customize any of the authentication flows from the **Authentication** menu. To do this, you can change the settings of the authentication flow definition associated with them or create your own using an existing one as a template. Once ready, bind a custom flow using the **⋮** (Actions) menu → **Bind flow**.

The easiest — and recommended — way to create a flow is to **Duplicate** an existing definition from the Authentication tab. The reason for this is that you can easily roll back your changes and switch to the definition you used as a template, in case the flow is broken by your changes.

#### Binding scope: realm level vs client level

A custom flow can be applied at two scopes:

| Scope | How to bind | Effect |
|---|---|---|
| **Realm** | **Authentication** → flow **⋮** menu → **Bind flow** → choose flow type (Browser, Direct grant, Registration…) | Every client in the realm uses this flow unless the client overrides it |
| **Client** | **Clients** → select client → **Advanced** tab → **Authentication flow overrides** section | Only that client uses the overriding flow; all other clients still use the realm default |

Use the client-level override when a specific application has different login requirements — for example, a back-office client that must always require OTP while the public app does not.

---

## Task 1 — Customize the browser authentication flow

> Estimated time: 15–20 min | Tools: admin console, browser

**Goal:** Duplicate the default browser flow, replace the combined username-password form with separate username and password steps, bind the new flow, and verify that users authenticate through a multi-step experience.

**Observable outcome:**
- The new flow `My Browser` appears in the Authentication list
- The flow contains `Username Form` followed by `Password Form`, both marked `REQUIRED`
- Logging in to the Keycloak account console presents the username and password on separate pages
- The original browser flow remains available as a fallback

<details>
<summary>Hint — what is the safest way to modify a built-in authentication flow without losing the original configuration?</summary>

Keycloak provides a specific action on existing flows that creates a copy. This copy can be edited and bound independently, leaving the original intact for rollback.

</details>

<details>
<summary>Hint — after creating the custom flow, how do you make it active for realm browser logins?</summary>

There is a bind action available from the flow's menu that associates it with a specific authentication context (such as browser, registration, or direct grant).

</details>

<details>
<summary>Solution — step-by-step walkthrough</summary>

First, in the left sidebar click **Authentication**. Click on the **browser** flow. Then, click the **⋮** (Actions) menu on the **browser** flow and select **Duplicate** to create a new flow based on it. You should be prompted to choose a name for the new flow. Let's name it **My Browser** and click **Duplicate**.

Now, let's change how users authenticate in the realm by gathering both the username and password in different steps and from different pages, instead of asking for the credentials using a single login page.

For that, locate the **Username Password Form** execution and click on **Delete** (Trash icon). At the moment, your flow should look as follows:

Now, let's add two steps to this flow to ask the user for the username and then ask for the password. For that, click on the **+** button menu on the right-hand side of the **Custom browser form** subflow and click on the **Add step** button.

A pop-up appears listing all available authentication executions, including Username Form, Password Form, OTP Form, WebAuthn Authenticator, and many others. In our case, we are going to select **Username Form** from the Provider select box and click on the **Add** button to add the execution to the subflow.

Once the execution has been added to the flow, you should see it within the subflow. By default, executions are added to the bottom of the flow, but in our case, we want this execution at the top of the subflow so that we can obtain the username first. For that, you can select-and-hold the **Username Form** and move it to the beginning of the subflow.

Perform the same steps you did previously to add the **Password Form** authentication step to the subflow to obtain the password and authenticate the user. Make sure **Password Form** is the second execution in the subflow.

Let's make sure that both the **Username Form** and **Password Form** executions are marked as **REQUIRED**. For that, click on the **REQUIRED** setting for each authentication execution. This is an important step as it forces our end users to provide both pieces of information when they're logging into the realm.

Now, the **My Browser** authentication flow should look like this:

```
My Browser
└── My Browser Browser - Conditional OTP  [CONDITIONAL]
    ├── Username Form                      [REQUIRED]
    └── Password Form                      [REQUIRED]
```

Finally, click on the three dot button of your new authentication flow and click on **Bind flow** to associate it with the **Browser flow**.

Now, let's try to log into the Keycloak account console. Open your browser at `https://labs-sso.keycloak.academy/realms/{realm}/account` and log in using your user credentials. When authenticating to the realm, you should notice that the username and password of the user are obtained and validated in multiple steps.

> **Note:** To restore the default behavior, bind the original **browser** flow back to the **Browser flow** context.

</details>

---

## Lab Checkpoint

Verify that all of the following are true before marking this lab complete:

- [ ] Logging in to the account console uses the multi-step username-then-password experience

---

## Going Further

These extension tasks have no hints or solutions. They are for learners who want to explore beyond the lab's core objectives.

- Add a Conditional OTP subflow to `My Browser` so that users with the `otp-configured` attribute are prompted for a TOTP code after the password step, while others skip it.
