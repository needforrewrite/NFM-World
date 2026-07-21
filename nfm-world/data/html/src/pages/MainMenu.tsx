import { useState, useEffect, useCallback } from "preact/hooks";
import { styled } from "goober";
import { callNfmw, onNfmwEvent } from "../shared/bridge";
import { AccountData } from "../shared/memorypack/AccountData";
import { Settings } from "./Settings";

// ── Types ────────────────────────────────────────────────────────

interface MenuItem {
  label: string;
  description: string;
  action: () => void;
}

interface MenuPage {
  title: string;
  items: MenuItem[];
}

// ── Styled components ────────────────────────────────────────────

const Root = styled("div")`
  width: 100%; height: 100%;
  display: flex; flex-direction: column;
  align-items: center; justify-content: center;
  animation: nfmw-fadeIn 0.3s ease-out;
`;

const PageTitle = styled("div")`
  font-size: 48px; font-weight: 800; letter-spacing: 4px;
  text-transform: uppercase; margin-bottom: 8px;
  text-shadow: 0 2px 16px rgba(79,195,247,0.4);
`;

const Subtitle = styled("div")`
  font-size: 14px; color: rgba(255,255,255,0.5);
  margin-bottom: 40px; letter-spacing: 2px;
`;

const Items = styled("div")`
  display: flex; flex-direction: column; gap: 6px; min-width: 320px;
`;

const ItemBtn = styled("button")`
  display: flex; flex-direction: column; align-items: flex-start;
  padding: 14px 24px; color: #fff;
  background: rgba(255,255,255,0.06);
  border: 1px solid rgba(255,255,255,0.1);
  border-radius: 8px; cursor: pointer; transition: all 0.15s ease;
  text-align: left; width: 100%;
  &:hover {
    background: rgba(79,195,247,0.12);
    border-color: rgba(79,195,247,0.25);
    transform: translateY(-1px);
  }
  &:active { transform: translateY(0); }
`;

const ItemLabel = styled("span")`
  font-size: 16px; font-weight: 600; letter-spacing: 1px;
`;

const ItemDesc = styled("span")`
  font-size: 12px; color: rgba(255,255,255,0.4);
  margin-top: 2px; letter-spacing: 0.5px;
`;

const BackBtn = styled("button")`
  margin-top: 12px; padding: 10px 24px; font-size: 14px; font-weight: 600;
  color: rgba(255,255,255,0.5); background: rgba(255,255,255,0.04);
  border: 1px solid rgba(255,255,255,0.08); border-radius: 6px;
  cursor: pointer; transition: all 0.15s ease;
  &:hover { color: #fff; background: rgba(255,255,255,0.08); }
`;

const Footer = styled("div")`
  position: absolute; bottom: 20px; font-size: 11px;
  color: rgba(255,255,255,0.3); letter-spacing: 1px;
`;

// ── Component ────────────────────────────────────────────────────

export function MainMenu() {
  const [account, setAccount] = useState<AccountData | null>(null);
  const [pageStack, setPageStack] = useState<MenuPage[]>([]);
  const [currentView, setCurrentView] = useState<"menu" | "settings">("menu");

  useEffect(() => {
    return onNfmwEvent<AccountData | null>("main-menu:account", setAccount, AccountData.deserialize.bind(AccountData));
  }, []);

  const goBack = useCallback(() => {
    setPageStack((s) => s.slice(0, -1));
  }, []);

  const pushPage = useCallback((page: MenuPage) => {
    setPageStack((s) => [...s, page]);
  }, []);

  // Build page factories
  const buildSpMenu = useCallback((): MenuPage => ({
    title: "SINGLEPLAYER",
    items: [
      { label: "NFM1", description: "Play the original NFM1 singleplayer campaign.", action: () => callNfmw("navigate", { page: "playNfm1" }) },
      { label: "NFM2", description: "Play the original NFM2 singleplayer campaign.", action: () => callNfmw("navigate", { page: "playNfm2" }) },
      { label: "COMMUNITY", description: "Play custom experiences crafted by the community.", action: () => callNfmw("navigate", { page: "playCommunity" }) },
      { label: "FREE PLAY", description: "Play freely without any restrictions.", action: () => callNfmw("navigate", { page: "play" }) },
      { label: "BACK", description: "Return to the previous menu.", action: goBack },
    ],
  }), [goBack]);

  const buildMpMenu = useCallback((): MenuPage => ({
    title: "MULTIPLAYER",
    items: [
      { label: "COMPETITIVE", description: "Compete against other players via matchmaking.", action: () => callNfmw("navigate", { page: "multiplayer" }) },
      { label: "CASUAL", description: "Play with people in a free relaxed environment.", action: () => callNfmw("navigate", { page: "casual" }) },
      { label: "BACK", description: "Return to the previous menu.", action: goBack },
    ],
  }), [goBack]);

  const buildWorkshopMenu = useCallback((): MenuPage => ({
    title: "WORKSHOP",
    items: [
      { label: "MODEL EDITOR", description: "View and edit custom models.", action: () => callNfmw("navigate", { page: "modelEditor" }) },
      { label: "STAGE EDITOR", description: "Design your own stages.", action: () => callNfmw("navigate", { page: "stageEditor" }) },
      { label: "CAMPAIGN EDITOR", description: "Craft custom experiences.", action: () => callNfmw("navigate", { page: "campaignEditor" }) },
      { label: "BACK", description: "Return to the previous menu.", action: goBack },
    ],
  }), [goBack]);

  const buildTrainingMenu = useCallback((): MenuPage => ({
    title: "TRAINING",
    items: [
      { label: "TIME TRIALS", description: "Flex your fastest time against other people.", action: () => callNfmw("navigate", { page: "timeTrials" }) },
      { label: "CHALLENGES", description: "Complete challenges to sharpen your mechanical skills.", action: () => callNfmw("navigate", { page: "challenges" }) },
      { label: "GAME INSTRUCTIONS", description: "Read about the rules and controls of the game.", action: () => callNfmw("navigate", { page: "gameInstructions" }) },
      { label: "BACK", description: "Return to the previous menu.", action: goBack },
    ],
  }), [goBack]);

  const buildPlayMenu = useCallback((): MenuPage => ({
    title: "PLAY",
    items: [
      { label: "SINGLEPLAYER", description: "Play the original single player experiences.", action: () => pushPage(buildSpMenu()) },
      { label: "MULTIPLAYER", description: "Play online with other players.", action: () => pushPage(buildMpMenu()) },
      { label: "TRAINING", description: "Train your skills and learn the game mechanics.", action: () => pushPage(buildTrainingMenu()) },
      { label: "BACK", description: "Return to the previous menu.", action: goBack },
    ],
  }), [goBack, buildSpMenu, buildMpMenu, buildTrainingMenu, pushPage]);

  // Main menu page
  const mainPage: MenuPage = {
    title: "NFM WORLD",
    items: [
      { label: "PLAY", description: "Play public, private matches online or play singleplayer.", action: () => pushPage(buildPlayMenu()) },
      { label: "GARAGE", description: "Customize and inspect your vehicles in the garage.", action: () => callNfmw("navigate", { page: "garage" }) },
      { label: "WORKSHOP", description: "Build your own models and stages.", action: () => pushPage(buildWorkshopMenu()) },
      { label: "SETTINGS", description: "Adjust game settings.", action: () => setCurrentView("settings") },
      { label: "CREDITS", description: "View game credits.", action: () => callNfmw("navigate", { page: "credits" }) },
      { label: "QUIT", description: "Exit the game.", action: () => callNfmw("navigate", { page: "quit" }) },
    ],
  };

  const currentPage = pageStack.length > 0 ? pageStack[pageStack.length - 1] : mainPage;
  const showBack = pageStack.length > 0;

  // ── Embedded Settings view ────────────────────────────────────
  if (currentView === "settings") {
    return <Settings onClose={() => setCurrentView("menu")} />;
  }

  return (
    <Root>
      <PageTitle>{currentPage.title}</PageTitle>
      <Subtitle>
        {pageStack.length === 0
          ? (account?.isLoggedIn ? `Welcome, ${account.name}` : "Racing Simulator")
          : ""}
      </Subtitle>
      <Items>
        {currentPage.items.map((item) => (
          <ItemBtn key={item.label} onClick={item.action}>
            <ItemLabel>{item.label}</ItemLabel>
            <ItemDesc>{item.description}</ItemDesc>
          </ItemBtn>
        ))}
        {showBack && <BackBtn onClick={goBack}>← BACK</BackBtn>}
      </Items>
      {pageStack.length === 0 && <Footer>NFM World — CEF + Preact UI</Footer>}
    </Root>
  );
}
