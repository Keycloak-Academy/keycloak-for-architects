# Examples for Chapter 13 - Extending Keycloak

> Requires **Keycloak 26+** and **Java 17+**.

## Themes

To build this project, execute the following command:

    ./mvnw clean package

Then copy the JAR to the providers directory and run the build step before starting Keycloak:

    cp target/mytheme.jar $KC_HOME/providers/
    $KC_HOME/bin/kc.sh build
    $KC_HOME/bin/kc.sh start-dev

### What is here

* A custom login theme called `mytheme` (extends the `keycloak.v2` PatternFly 4 base theme).
* An example on how to use a `ThemeSelectorProvider` to dynamically choose a theme.
* An example on how to use a `MyThemeResourceProvider` to load additional templates and resources.