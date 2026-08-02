import { h, render, FunctionComponent } from "preact";
import { useState, useEffect } from "preact/hooks";
import { setup } from "goober";
import { MainMenu } from "./pages/MainMenu";
import { Garage } from "./pages/Garage";
import { RaceHud } from "./pages/RaceHud";
import { TestPage } from "./pages/Test";
import { PauseMenu } from "./pages/PauseMenu";
import { onNfmwEvent } from "./shared/bridge";

// Wire goober to Preact's h function — must run before any styled() calls.
setup(h);

// ── Simple hash-based router ─────────────────────────────────────
// Reads window.location.hash to determine which page component to
// render. Hash changes (e.g. #/garage) are handled without page
// reload, keeping the CEF render process alive.

const routes: Record<string, FunctionComponent> = {
  "#/main-menu": MainMenu,
  "#/garage": Garage,
  "#/race": RaceHud,
  "#/pause": PauseMenu,
  "#/test": TestPage,
  '#/empty': () => <div style={{ width: "100%", height: "100%" }}></div>
};

const defaultRoute = "#/main-menu";

function Router() {
  const [hash, setHash] = useState(
    window.location.hash || defaultRoute,
  );

  useEffect(() => {
    const onHashChange = () => setHash(window.location.hash || defaultRoute);
    window.addEventListener("hashchange", onHashChange);
    return () => window.removeEventListener("hashchange", onHashChange);
  }, []);

  // Also listen for programmatic hash changes pushed from C#
  useEffect(() => {
    return onNfmwEvent<string>("nfmw:navigate", (newHash) => {
      window.location.hash = newHash;
    });
  }, []);

  const Page = routes[hash] ?? routes[defaultRoute];
  return <Page />;
}

function App() {
  return <Router />;
}

render(<App />, document.getElementById("app")!);
