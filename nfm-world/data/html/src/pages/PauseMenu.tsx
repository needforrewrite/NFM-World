import { useState, useEffect } from "preact/hooks";
import { styled } from "goober";
import { callNfmw, onNfmwEvent } from "../shared/bridge";
import { Settings } from "./Settings";

// ── Types ────────────────────────────────────────────────────────

interface PauseState {
  lap: number;
  totalLaps: number;
  position: number;
  totalRacers: number;
  stageName: string;
}

type PauseView = "menu" | "settings" | "confirmQuit" | "confirmRestart";

interface PauseMenuProps {
  /** Whether the pause overlay is visible. */
  visible: boolean;
}

// ── Styled components ────────────────────────────────────────────

const Overlay = styled("div")`
  position: absolute; inset: 0;
  display: flex; align-items: center; justify-content: center;
  background: rgba(0,0,0,0.65);
  z-index: 50;
  animation: nfmw-fadeIn 0.2s ease-out;
`;

const MenuCard = styled("div")`
  width: 380px;
  padding: 36px 40px;
  background: rgba(20,20,40,0.85);
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  border: 1px solid rgba(255,255,255,0.1);
  border-radius: 16px;
  display: flex; flex-direction: column; align-items: center; gap: 6px;
  box-shadow: 0 8px 40px rgba(0,0,0,0.5);
`;

const Title = styled("div")`
  font-size: 22px; font-weight: 700; letter-spacing: 4px;
  text-transform: uppercase; margin-bottom: 4px;
  text-shadow: 0 2px 12px rgba(79,195,247,0.3);
`;

const Subtitle = styled("div")`
  font-size: 12px; color: rgba(255,255,255,0.35);
  letter-spacing: 1.5px; text-transform: uppercase;
  margin-bottom: 20px;
`;

const RaceInfo = styled("div")`
  width: 100%;
  display: flex; justify-content: center; gap: 32px;
  margin-bottom: 20px;
  padding: 10px 0;
  border-top: 1px solid rgba(255,255,255,0.06);
  border-bottom: 1px solid rgba(255,255,255,0.06);
`;

const InfoItem = styled("div")`
  display: flex; flex-direction: column; align-items: center;
  gap: 2px;
`;

const InfoLabel = styled("div")`
  font-size: 10px; color: rgba(255,255,255,0.35);
  letter-spacing: 1.5px; text-transform: uppercase;
`;

const InfoValue = styled("div")`
  font-size: 18px; font-weight: 700;
  font-variant-numeric: tabular-nums;
`;

const MenuBtn = styled("button")<{ variant?: "primary" | "danger" }>`
  width: 100%; padding: 14px 24px;
  font-size: 15px; font-weight: 600;
  letter-spacing: 2px; text-transform: uppercase;
  color: #fff;
  background: ${(p) =>
    p.variant === "danger"
      ? "rgba(255,82,82,0.12)"
      : p.variant === "primary"
        ? "rgba(79,195,247,0.15)"
        : "rgba(255,255,255,0.05)"};
  border: 1px solid
    ${(p) =>
      p.variant === "danger"
        ? "rgba(255,82,82,0.25)"
        : "rgba(255,255,255,0.1)"};
  border-radius: 8px; cursor: pointer;
  transition: all 0.15s ease;
  &:hover {
    background: ${(p) =>
      p.variant === "danger"
        ? "rgba(255,82,82,0.22)"
        : p.variant === "primary"
          ? "rgba(79,195,247,0.25)"
          : "rgba(255,255,255,0.1)"};
    border-color: ${(p) =>
      p.variant === "danger"
        ? "rgba(255,82,82,0.4)"
        : "rgba(255,255,255,0.2)"};
  }
`;

// ── Confirm dialog ───────────────────────────────────────────────

const ConfirmOverlay = styled("div")`
  position: fixed; inset: 0;
  display: flex; align-items: center; justify-content: center;
  background: rgba(0,0,0,0.7);
  z-index: 60;
  animation: nfmw-fadeIn 0.15s ease-out;
`;

const ConfirmBox = styled("div")`
  background: #1a1a2e; border: 1px solid rgba(255,255,255,0.12);
  border-radius: 10px; padding: 24px 28px; min-width: 320px;
  text-align: center;
  box-shadow: 0 8px 32px rgba(0,0,0,0.5);
`;

const ConfirmTitle = styled("div")`
  font-size: 16px; font-weight: 700; letter-spacing: 1.5px;
  text-transform: uppercase; margin-bottom: 8px;
`;

const ConfirmMsg = styled("div")`
  font-size: 13px; color: rgba(255,255,255,0.45);
  margin-bottom: 20px; line-height: 1.5;
`;

const ConfirmBtns = styled("div")`
  display: flex; justify-content: center; gap: 10px;
`;

const ConfirmBtn = styled("button")<{ variant?: "primary" | "danger" }>`
  padding: 10px 28px; font-size: 13px; font-weight: 600;
  letter-spacing: 1px; text-transform: uppercase;
  color: #fff; cursor: pointer;
  background: ${(p) =>
    p.variant === "danger"
      ? "rgba(255,82,82,0.18)"
      : "rgba(255,255,255,0.06)"};
  border: 1px solid
    ${(p) =>
      p.variant === "danger"
        ? "rgba(255,82,82,0.3)"
        : "rgba(255,255,255,0.12)"};
  border-radius: 6px;
  transition: all 0.15s ease;
  &:hover {
    background: ${(p) =>
      p.variant === "danger"
        ? "rgba(255,82,82,0.28)"
        : "rgba(255,255,255,0.12)"};
  }
`;

// ── Component ────────────────────────────────────────────────────

/**
 * Pause menu overlay. Controlled by the `visible` prop — the parent
 * (RaceHud) shows/hides it based on the `"race:paused"` event from C#.
 *
 * Renders on top of the race HUD within the same `#/race` route, so
 * HUD state is preserved across pause/resume cycles.
 */
export function PauseMenu({ visible }: PauseMenuProps) {
  const [view, setView] = useState<PauseView>("menu");
  const [pauseState, setPauseState] = useState<PauseState | null>(null);

  // Listen for pause context pushed from C#
  useEffect(() => {
    return onNfmwEvent<PauseState>("race:pauseState", (data) => {
      setPauseState(data);
    });
  }, []);

  // Reset sub-view when visibility changes
  useEffect(() => {
    if (!visible) setView("menu");
  }, [visible]);

  // Notify C# when settings sub-view opens/closes so Escape can be
  // routed correctly (dismiss settings vs. resume race).
  useEffect(() => {
    if (!visible) return;
    if (view === "settings") {
      callNfmw("settingsOpened");
    } else {
      callNfmw("settingsClosed");
    }
  }, [view, visible]);

  // Handle Escape key to dismiss settings sub-view (CEF input is
  // enabled during pause, so Escape reaches the browser).
  useEffect(() => {
    if (!visible || view !== "settings") return;

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        e.stopPropagation();
        setView("menu");
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [view, visible]);

  if (!visible) return null;

  // ── Settings sub-view ──────────────────────────────────────────
  if (view === "settings") {
    return (
      <div style={{ position: "absolute", inset: 0, zIndex: 50 }}>
        <Settings onClose={() => setView("menu")} />
      </div>
    );
  }

  // ── Confirm dialogs ────────────────────────────────────────────
  if (view === "confirmQuit") {
    return (
      <Overlay>
        <ConfirmOverlay onClick={() => setView("menu")}>
          <ConfirmBox onClick={(e) => e.stopPropagation()}>
            <ConfirmTitle>Quit to Main Menu?</ConfirmTitle>
            <ConfirmMsg>
              All race progress will be lost.
            </ConfirmMsg>
            <ConfirmBtns>
              <ConfirmBtn onClick={() => setView("menu")}>Cancel</ConfirmBtn>
              <ConfirmBtn variant="danger" onClick={() => callNfmw("quit")}>
                Quit
              </ConfirmBtn>
            </ConfirmBtns>
          </ConfirmBox>
        </ConfirmOverlay>
      </Overlay>
    );
  }

  if (view === "confirmRestart") {
    return (
      <Overlay>
        <ConfirmOverlay onClick={() => setView("menu")}>
          <ConfirmBox onClick={(e) => e.stopPropagation()}>
            <ConfirmTitle>Restart Race?</ConfirmTitle>
            <ConfirmMsg>
              Your current lap time and position will be reset.
            </ConfirmMsg>
            <ConfirmBtns>
              <ConfirmBtn onClick={() => setView("menu")}>Cancel</ConfirmBtn>
              <ConfirmBtn variant="danger" onClick={() => callNfmw("restart")}>
                Restart
              </ConfirmBtn>
            </ConfirmBtns>
          </ConfirmBox>
        </ConfirmOverlay>
      </Overlay>
    );
  }

  // ── Main pause menu ────────────────────────────────────────────
  return (
    <Overlay>
      <MenuCard>
        <Title>Paused</Title>
        <Subtitle>{pauseState?.stageName ?? "Race"}</Subtitle>

        {pauseState && (
          <RaceInfo>
            <InfoItem>
              <InfoLabel>Lap</InfoLabel>
              <InfoValue>{pauseState.lap}/{pauseState.totalLaps}</InfoValue>
            </InfoItem>
            <InfoItem>
              <InfoLabel>Position</InfoLabel>
              <InfoValue>{pauseState.position}/{pauseState.totalRacers}</InfoValue>
            </InfoItem>
          </RaceInfo>
        )}

        <MenuBtn variant="primary" onClick={() => callNfmw("resume")}>
          Resume
        </MenuBtn>
        <MenuBtn onClick={() => setView("confirmRestart")}>
          Restart
        </MenuBtn>
        <MenuBtn onClick={() => setView("settings")}>
          Settings
        </MenuBtn>
        <MenuBtn variant="danger" onClick={() => setView("confirmQuit")}>
          Quit to Main Menu
        </MenuBtn>
      </MenuCard>
    </Overlay>
  );
}
