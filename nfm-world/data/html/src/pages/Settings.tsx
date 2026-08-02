import { useState, useEffect, useCallback, useRef } from "preact/hooks";
import { styled } from "goober";
import { callNfmw, onNfmwEvent } from "../shared/bridge";
import { SettingsSnapshot } from "../shared/memorypack/SettingsSnapshot";
import { AvailableOptions } from "../shared/memorypack/AvailableOptions";
import { KeyBindingData } from "../shared/memorypack/KeyBindingData";

// ── Types ────────────────────────────────────────────────────────

type Tab = "keyboard" | "video" | "audio" | "game";

const TAB_LABELS: Record<Tab, string> = {
  keyboard: "Keyboard",
  video: "Video",
  audio: "Audio",
  game: "Game",
};

const TABS: Tab[] = ["keyboard", "video", "audio", "game"];

// ── Styled components ────────────────────────────────────────────

const Root = styled("div")`
  width: 100%; height: 100%;
  display: flex; flex-direction: column;
  animation: nfmw-fadeIn 0.25s ease-out;
  background: rgba(0,0,0,0.65);
`;

const Header = styled("div")`
  display: flex; align-items: center; justify-content: center;
  padding: 20px 0 0 0;
  font-size: 28px; font-weight: 700; letter-spacing: 3px;
  text-transform: uppercase;
  text-shadow: 0 2px 12px rgba(79,195,247,0.3);
`;

const TabBar = styled("div")`
  display: flex; justify-content: center; gap: 4px;
  padding: 16px 40px 0 40px;
`;

const TabBtn = styled("button")<{ active: boolean }>`
  padding: 8px 20px; font-size: 13px; font-weight: 600;
  letter-spacing: 1.5px; text-transform: uppercase;
  color: ${(p) => (p.active ? "#fff" : "rgba(255,255,255,0.45)")};
  background: ${(p) => (p.active ? "rgba(79,195,247,0.18)" : "transparent")};
  border: 1px solid ${(p) => (p.active ? "rgba(79,195,247,0.35)" : "transparent")};
  border-radius: 6px; cursor: pointer;
  transition: all 0.15s ease;
  &:hover {
    color: #fff;
    background: rgba(79,195,247,0.1);
  }
`;

const Content = styled("div")`
  flex: 1; overflow-y: auto; padding: 20px 40px 10px 40px;
`;

const BottomBar = styled("div")`
  display: flex; align-items: center; justify-content: center; gap: 12px;
  padding: 16px 40px;
  border-top: 1px solid rgba(255,255,255,0.08);
`;

const Btn = styled("button")<{ variant?: "primary" | "secondary" }>`
  padding: 10px 28px; font-size: 13px; font-weight: 600;
  letter-spacing: 1px; text-transform: uppercase;
  color: #fff;
  background: ${(p) =>
    p.variant === "primary"
      ? "rgba(79,195,247,0.22)"
      : "rgba(255,255,255,0.06)"};
  border: 1px solid
    ${(p) =>
      p.variant === "primary"
        ? "rgba(79,195,247,0.35)"
        : "rgba(255,255,255,0.12)"};
  border-radius: 6px; cursor: pointer;
  transition: all 0.15s ease;
  &:hover {
    background: ${(p) =>
      p.variant === "primary"
        ? "rgba(79,195,247,0.32)"
        : "rgba(255,255,255,0.12)"};
  }
`;

const SectionLabel = styled("div")`
  font-size: 11px; font-weight: 600; letter-spacing: 2px;
  text-transform: uppercase; color: rgba(255,255,255,0.35);
  margin-bottom: 10px;
`;

const Row = styled("div")`
  display: flex; align-items: center; justify-content: space-between;
  padding: 6px 0; gap: 16px;
`;

const Label = styled("span")`
  font-size: 14px; color: rgba(255,255,255,0.8);
  min-width: 140px;
`;

const Select = styled("select")`
  padding: 6px 10px; font-size: 13px;
  background: rgba(255,255,255,0.06);
  color: #fff; border: 1px solid rgba(255,255,255,0.15);
  border-radius: 5px; cursor: pointer; min-width: 160px;
  outline: none;
  &:focus { border-color: rgba(79,195,247,0.4); }
  option { background: #1a1a2e; color: #fff; }
`;

const Checkbox = styled("input")`
  width: 16px; height: 16px; cursor: pointer; accent-color: #4fc3f7;
`;

const Slider = styled("input")`
  width: 160px; cursor: pointer; accent-color: #4fc3f7;
`;

const SliderValue = styled("span")`
  font-size: 12px; color: rgba(255,255,255,0.5);
  margin-left: 8px; min-width: 50px;
`;

const KeyBindingRow = styled("div")`
  display: flex; align-items: center; justify-content: space-between;
  padding: 4px 0; gap: 12px;
`;

const KeyBindingLabel = styled("span")`
  font-size: 13px; color: rgba(255,255,255,0.75);
  min-width: 160px;
`;

const KeyBindingBtn = styled("button")<{ capturing: boolean }>`
  padding: 5px 18px; font-size: 13px; font-weight: 600;
  color: #fff; min-width: 140px; text-align: center;
  background: ${(p) =>
    p.capturing
      ? "rgba(255,152,0,0.25)"
      : "rgba(255,255,255,0.06)"};
  border: 1px solid
    ${(p) =>
      p.capturing
        ? "rgba(255,152,0,0.5)"
        : "rgba(255,255,255,0.12)"};
  border-radius: 5px; cursor: pointer;
  transition: all 0.15s ease;
  &:hover {
    background: ${(p) =>
      p.capturing
        ? "rgba(255,152,0,0.35)"
        : "rgba(255,255,255,0.12)"};
  }
`;

const ResetLink = styled("button")`
  background: none; border: none; color: rgba(255,255,255,0.35);
  font-size: 12px; cursor: pointer; letter-spacing: 1px;
  text-transform: uppercase; padding: 8px 0;
  &:hover { color: rgba(255,152,0,0.7); }
`;

const Divider = styled("div")`
  height: 1px; background: rgba(255,255,255,0.06);
  margin: 12px 0;
`;

// ── Confirm Modal ────────────────────────────────────────────────

const ModalOverlay = styled("div")`
  position: fixed; inset: 0;
  display: flex; align-items: center; justify-content: center;
  background: rgba(0,0,0,0.5);
  z-index: 100;
  animation: nfmw-fadeIn 0.15s ease-out;
`;

const ModalBox = styled("div")`
  background: #1a1a2e; border: 1px solid rgba(255,255,255,0.12);
  border-radius: 10px; padding: 24px 28px; min-width: 320px;
  text-align: center;
  box-shadow: 0 8px 32px rgba(0,0,0,0.5);
`;

const ModalTitle = styled("div")`
  font-size: 16px; font-weight: 700; letter-spacing: 1.5px;
  text-transform: uppercase; margin-bottom: 8px;
`;

const ModalMessage = styled("div")`
  font-size: 13px; color: rgba(255,255,255,0.5);
  margin-bottom: 20px;
`;

const ModalBtns = styled("div")`
  display: flex; justify-content: center; gap: 10px;
`;

// ── Helpers ──────────────────────────────────────────────────────

function keyCodeToName(code: number): string {
  if (code === 0) return "None";
  // Common mappings — the SDL keycode values match Windows virtual key codes
  const map: Record<number, string> = {
    0x08: "Backspace", 0x09: "Tab", 0x0d: "Enter",
    0x10: "Shift", 0x11: "Ctrl", 0x12: "Alt",
    0x1b: "Escape", 0x20: "Space",
    0x21: "PageUp", 0x22: "PageDown", 0x23: "End", 0x24: "Home",
    0x25: "Left", 0x26: "Up", 0x27: "Right", 0x28: "Down",
    0x2c: "PrintScreen", 0x2d: "Insert", 0x2e: "Delete",
    0x5b: "LWin", 0x5c: "RWin",
    0x70: "F1", 0x71: "F2", 0x72: "F3", 0x73: "F4",
    0x74: "F5", 0x75: "F6", 0x76: "F7", 0x77: "F8",
    0x78: "F9", 0x79: "F10", 0x7a: "F11", 0x7b: "F12",
    0x90: "NumLock", 0x91: "ScrollLock",
    0xa0: "LShift", 0xa1: "RShift",
    0xa2: "LCtrl", 0xa3: "RCtrl",
    0xa4: "LAlt", 0xa5: "RAlt",
    0xba: ";", 0xbb: "=", 0xbc: ",", 0xbd: "-", 0xbe: ".", 0xbf: "/",
    0xc0: "`",
    0xdb: "[", 0xdc: "\\", 0xdd: "]", 0xde: "'",
  };
  if (map[code]) return map[code];
  // Letters A-Z
  if (code >= 0x41 && code <= 0x5a) return String.fromCharCode(code);
  // Numbers 0-9
  if (code >= 0x30 && code <= 0x39) return String.fromCharCode(code);
  // Numpad
  if (code >= 0x60 && code <= 0x69) return `NumPad${code - 0x60}`;
  if (code === 0x6a) return "NumPad*";
  if (code === 0x6b) return "NumPad+";
  if (code === 0x6d) return "NumPad-";
  if (code === 0x6e) return "NumPad.";
  if (code === 0x6f) return "NumPad/";
  return `Key(0x${code.toString(16)})`;
}

// ── Component ────────────────────────────────────────────────────

interface SettingsProps {
  /** Called when the user dismisses settings (Cancel/OK). */
  onClose?: () => void;
}

export function Settings({ onClose }: SettingsProps) {
  const [tab, setTab] = useState<Tab>("video");
  const [config, setConfig] = useState<SettingsSnapshot | null>(null);
  const [options, setOptions] = useState<AvailableOptions | null>(null);
  const [capturingAction, setCapturingAction] = useState<string | null>(null);
  const [confirmModal, setConfirmModal] = useState<{
    title: string; message: string; onConfirm: () => void;
  } | null>(null);
  const [restartModal, setRestartModal] = useState(false);
  const _pendingCloseRef = useRef(false);

  // Listen for initial config push from C#
  useEffect(() => {
    const unsubConfig = onNfmwEvent<SettingsSnapshot | null>(
      "settings:config",
      (data) => setConfig(data),
      SettingsSnapshot.deserialize.bind(SettingsSnapshot)
    );
    const unsubOptions = onNfmwEvent<AvailableOptions | null>(
      "settings:options",
      (data) => setOptions(data),
      AvailableOptions.deserialize.bind(AvailableOptions)
    );
    // Listen for key capture results
    const unsubKey = onNfmwEvent<{
      action: string | null; keyCode: number; cancelled: boolean;
    }>(
      "settings:keyCaptured",
      (data) => {
        setCapturingAction(null);
        if (!data.cancelled && data.action) {
          updateConfig((c) => {
            const bindings = c.keyBindings?.map((b) => {
              if (!b) return b;
              if (b.keyCode === data.keyCode && b.action !== data.action) {
                return { ...b, keyCode: 0 };
              }
              if (b.action === data.action) {
                return { ...b, keyCode: data.keyCode };
              }
              return b;
            }) ?? [];
            return { ...c, keyBindings: bindings };
          });
        }
      }
    );
    // Listen for restart requirement from C#
    const unsubRestart = onNfmwEvent<boolean>(
      "settings:requireRestart",
      () => setRestartModal(true)
    );
    // Listen for save-complete — if OK was clicked, auto-close
    const unsubSaved = onNfmwEvent<boolean>(
      "settings:saved",
      () => {
        if (_pendingCloseRef.current) {
          _pendingCloseRef.current = false;
          onClose?.();
        }
      }
    );
    // Request config from C#
    callNfmw("getConfig");

    return () => { unsubConfig(); unsubOptions(); unsubKey(); unsubRestart(); unsubSaved(); };
  }, []);

  // Apply a single setting change immediately (live preview)
  const updateConfig = useCallback(
    (updater: (prev: SettingsSnapshot) => SettingsSnapshot) => {
      setConfig((prev) => {
        if (!prev) return prev;
        const next = updater(prev);
        return next;
      });
    },
    []
  );

  const applySetting = useCallback((key: string, value: unknown) => {
    callNfmw("applySetting", { key, value });
  }, []);

  const handleSave = useCallback(() => {
    callNfmw("saveConfig");
  }, []);

  const handleClose = useCallback(() => {
    callNfmw("close");
    onClose?.();
  }, [onClose]);

  const handleOk = useCallback(() => {
    callNfmw("saveConfig");
    // Close is deferred — bridge pushes "saved" (no restart needed)
    // or "requireRestart" (dialog appears). "saved" triggers auto-close.
    _pendingCloseRef.current = true;
  }, []);

  const handleResetDefaults = useCallback(
    (section: string, title: string, message: string) => {
      setConfirmModal({
        title,
        message,
        onConfirm: () => {
          callNfmw("resetDefaults", { section });
          setConfirmModal(null);
        },
      });
    },
    []
  );

  if (!config || !options) {
    return (
      <Root>
        <Header>Settings</Header>
        <Content>
          <div
            style={{
              textAlign: "center",
              color: "rgba(255,255,255,0.3)",
              marginTop: 80,
            }}
          >
            Loading settings...
          </div>
        </Content>
      </Root>
    );
  }

  return (
    <Root>
      <Header>Settings</Header>

      <TabBar>
        {TABS.map((t) => (
          <TabBtn key={t} active={tab === t} onClick={() => setTab(t)}>
            {TAB_LABELS[t]}
          </TabBtn>
        ))}
      </TabBar>

      <Content>
        {tab === "video" && (
          <VideoTab
            config={config}
            options={options}
            updateConfig={updateConfig}
            applySetting={applySetting}
          />
        )}
        {tab === "audio" && (
          <AudioTab
            config={config}
            updateConfig={updateConfig}
            applySetting={applySetting}
          />
        )}
        {tab === "game" && (
          <GameTab
            config={config}
            updateConfig={updateConfig}
            applySetting={applySetting}
            onReset={() =>
              handleResetDefaults(
                "camera",
                "Reset Camera",
                "Are you sure you want to reset camera settings to default?"
              )
            }
          />
        )}
        {tab === "keyboard" && (
          <KeyboardTab
            config={config}
            capturingAction={capturingAction}
            setCapturingAction={setCapturingAction}
            onReset={() =>
              handleResetDefaults(
                "keyboard",
                "Reset Key Binds",
                "Are you sure you want to reset key binds to default?"
              )
            }
          />
        )}
      </Content>

      <BottomBar>
        <Btn variant="primary" onClick={handleOk}>
          OK
        </Btn>
        <Btn onClick={handleClose}>Cancel</Btn>
        <Btn onClick={handleSave}>Apply</Btn>
      </BottomBar>

      {confirmModal && !restartModal && (
        <ModalOverlay onClick={() => setConfirmModal(null)}>
          <ModalBox onClick={(e: Event) => e.stopPropagation()}>
            <ModalTitle>{confirmModal.title}</ModalTitle>
            <ModalMessage>{confirmModal.message}</ModalMessage>
            <ModalBtns>
              <Btn variant="primary" onClick={confirmModal.onConfirm}>
                Yes
              </Btn>
              <Btn onClick={() => setConfirmModal(null)}>No</Btn>
            </ModalBtns>
          </ModalBox>
        </ModalOverlay>
      )}

      {restartModal && (
        <ModalOverlay>
          <ModalBox onClick={(e: Event) => e.stopPropagation()}>
            <ModalTitle>Restart Required</ModalTitle>
            <ModalMessage>
              The renderer has been changed. A restart is required for this
              change to take effect.
            </ModalMessage>
            <ModalBtns>
              <Btn variant="primary" onClick={() => callNfmw("restartNow")}>
                Restart Now
              </Btn>
              <Btn onClick={() => {
                setRestartModal(false);
                if (_pendingCloseRef.current) {
                  _pendingCloseRef.current = false;
                  onClose?.();
                }
              }}>
                Later
              </Btn>
            </ModalBtns>
          </ModalBox>
        </ModalOverlay>
      )}
    </Root>
  );
}

// ── Video Tab ────────────────────────────────────────────────────

const DISTANT_OUTLINE_BEHAVIORS = [
  "Distance Falloff (With Cutoff)",
  "Distance Falloff",
  "Classic Cutoff (NFM)",
  "Always Render",
  "Hide Outlines",
];

function VideoTab({
  config,
  options,
  updateConfig,
  applySetting,
}: {
  config: SettingsSnapshot;
  options: AvailableOptions;
  updateConfig: (u: (c: SettingsSnapshot) => SettingsSnapshot) => void;
  applySetting: (key: string, value: unknown) => void;
}) {
  const set = (key: string, value: unknown) => {
    updateConfig((c) => ({ ...c, [key]: value }));
    applySetting(key, value);
  };

  return (
    <div>
      <SectionLabel>Display</SectionLabel>
      <Row>
        <Label>Renderer</Label>
        <Select
          value={config.selectedRenderer}
          onChange={(e: Event) =>
            set("selectedRenderer", Number((e.target as HTMLSelectElement).value))
          }
        >
          {(options.renderers ?? []).map((r, i) => (
            <option key={r} value={i}>
              {r}
            </option>
          ))}
        </Select>
      </Row>
      <Row>
        <Label>Resolution</Label>
        <Select
          value={config.selectedResolution}
          onChange={(e: Event) =>
            set(
              "selectedResolution",
              Number((e.target as HTMLSelectElement).value)
            )
          }
        >
          {(options.resolutions ?? []).map((r, i) => (
            <option key={r} value={i}>
              {r}
            </option>
          ))}
        </Select>
      </Row>
      <Row>
        <Label>Display Mode</Label>
        <Select
          value={config.selectedDisplayMode}
          onChange={(e: Event) =>
            set(
              "selectedDisplayMode",
              Number((e.target as HTMLSelectElement).value)
            )
          }
        >
          {(options.displayModes ?? []).map((m, i) => (
            <option key={m} value={i}>
              {m}
            </option>
          ))}
        </Select>
      </Row>
      <Row>
        <Label>VSync</Label>
        <Checkbox
          type="checkbox"
          checked={config.vsync}
          onChange={(e: Event) =>
            set("vsync", (e.target as HTMLInputElement).checked)
          }
        />
      </Row>
      <Row>
        <Label>FPS Limit</Label>
        <Slider
          type="range"
          min={0}
          max={240}
          value={config.fpsLimit}
          onChange={(e: Event) =>
            set("fpsLimit", Number((e.target as HTMLInputElement).value))
          }
        />
        <SliderValue>
          {config.fpsLimit === 0 ? "Unlimited" : `${config.fpsLimit} FPS`}
        </SliderValue>
      </Row>

      <Divider />
      <SectionLabel>Quality</SectionLabel>
      <Row>
        <Label>Antialiasing</Label>
        <Select
          value={config.antialias}
          onChange={(e: Event) =>
            set("antialias", Number((e.target as HTMLSelectElement).value))
          }
        >
          {(options.antialiasModes ?? []).map((a, i) => (
            <option key={a} value={i}>
              {a}
            </option>
          ))}
        </Select>
      </Row>
      <Row>
        <Label>Shadow Distance</Label>
        <Select
          value={config.shadowCascadeLevel}
          onChange={(e: Event) =>
            set(
              "shadowCascadeLevel",
              Number((e.target as HTMLSelectElement).value)
            )
          }
        >
          {(options.shadowCascadeLevels ?? []).map((s, i) => (
            <option key={s} value={i}>
              {s}
            </option>
          ))}
        </Select>
      </Row>
      <Row>
        <Label>Shadow Resolution</Label>
        <Select
          value={config.shadowResolution}
          onChange={(e: Event) =>
            set(
              "shadowResolution",
              Number((e.target as HTMLSelectElement).value)
            )
          }
        >
          {(options.shadowResolutions ?? []).map((s, i) => (
            <option key={s} value={i}>
              {s}
            </option>
          ))}
        </Select>
      </Row>
      <Row>
        <Label>Render Distance</Label>
        <Select
          value={config.renderDistance}
          onChange={(e: Event) =>
            set(
              "renderDistance",
              Number((e.target as HTMLSelectElement).value)
            )
          }
        >
          {(options.renderDistanceNames ?? []).map((d, i) => (
            <option key={d} value={i}>
              {d}
            </option>
          ))}
        </Select>
      </Row>

      <Divider />
      <SectionLabel>Advanced</SectionLabel>
      <Row>
        <Label>Low Latency</Label>
        <Checkbox
          type="checkbox"
          checked={config.lowLatency}
          onChange={(e: Event) =>
            set("lowLatency", (e.target as HTMLInputElement).checked)
          }
        />
      </Row>
      <Row>
        <Label>Outline Width</Label>
        <Slider
          type="range"
          min={0.5}
          max={4}
          step={0.1}
          value={config.lineWidth}
          onChange={(e: Event) =>
            set("lineWidth", Number((e.target as HTMLInputElement).value))
          }
        />
        <SliderValue>{config.lineWidth.toFixed(1)}</SliderValue>
      </Row>
      <Row>
        <Label>Distant Outlines</Label>
        <Select
          value={config.distantOutlineBehavior}
          onChange={(e: Event) =>
            set(
              "distantOutlineBehavior",
              Number((e.target as HTMLSelectElement).value)
            )
          }
        >
          {DISTANT_OUTLINE_BEHAVIORS.map((label, value) => (
            <option key={label} value={value}>
              {label}
            </option>
          ))}
        </Select>
      </Row>
    </div>
  );
}

// ── Audio Tab ────────────────────────────────────────────────────

function AudioTab({
  config,
  updateConfig,
  applySetting,
}: {
  config: SettingsSnapshot;
  updateConfig: (u: (c: SettingsSnapshot) => SettingsSnapshot) => void;
  applySetting: (key: string, value: unknown) => void;
}) {
  const set = (key: string, value: unknown) => {
    updateConfig((c) => ({ ...c, [key]: value }));
    applySetting(key, value);
  };

  return (
    <div>
      <SectionLabel>Volume</SectionLabel>
      <Row>
        <Label>Mute All</Label>
        <Checkbox
          type="checkbox"
          checked={config.muteAll}
          onChange={(e: Event) =>
            set("muteAll", (e.target as HTMLInputElement).checked)
          }
        />
      </Row>
      <Row>
        <Label>Master Volume</Label>
        <Slider
          type="range"
          min={0}
          max={1}
          step={0.01}
          value={config.masterVolume}
          onChange={(e: Event) =>
            set("masterVolume", Number((e.target as HTMLInputElement).value))
          }
        />
        <SliderValue>{Math.round(config.masterVolume * 100)}%</SliderValue>
      </Row>
      <Row>
        <Label>Music Volume</Label>
        <Slider
          type="range"
          min={0}
          max={1}
          step={0.01}
          value={config.musicVolume}
          onChange={(e: Event) =>
            set("musicVolume", Number((e.target as HTMLInputElement).value))
          }
        />
        <SliderValue>{Math.round(config.musicVolume * 100)}%</SliderValue>
      </Row>
      <Row>
        <Label>Effects Volume</Label>
        <Slider
          type="range"
          min={0}
          max={1}
          step={0.01}
          value={config.effectsVolume}
          onChange={(e: Event) =>
            set("effectsVolume", Number((e.target as HTMLInputElement).value))
          }
        />
        <SliderValue>{Math.round(config.effectsVolume * 100)}%</SliderValue>
      </Row>

      <Divider />
      <Row>
        <Label>Remastered Music</Label>
        <Checkbox
          type="checkbox"
          checked={config.remasteredMusic}
          onChange={(e: Event) =>
            set("remasteredMusic", (e.target as HTMLInputElement).checked)
          }
        />
      </Row>
    </div>
  );
}

// ── Game (Camera) Tab ────────────────────────────────────────────

function GameTab({
  config,
  updateConfig,
  applySetting,
  onReset,
}: {
  config: SettingsSnapshot;
  updateConfig: (u: (c: SettingsSnapshot) => SettingsSnapshot) => void;
  applySetting: (key: string, value: unknown) => void;
  onReset: () => void;
}) {
  const set = (key: string, value: unknown) => {
    updateConfig((c) => ({ ...c, [key]: value }));
    applySetting(key, value);
  };

  return (
    <div>
      <SectionLabel>Camera</SectionLabel>
      <Row>
        <Label>Field of View</Label>
        <Slider
          type="range"
          min={58.7}
          max={120}
          step={0.5}
          value={config.fov}
          onChange={(e: Event) =>
            set("fov", Number((e.target as HTMLInputElement).value))
          }
        />
        <SliderValue>{config.fov.toFixed(1)}°</SliderValue>
      </Row>
      <Row>
        <Label>Smooth FOV</Label>
        <Checkbox
          type="checkbox"
          checked={config.smoothFov}
          onChange={(e: Event) =>
            set("smoothFov", (e.target as HTMLInputElement).checked)
          }
        />
      </Row>
      <Row>
        <Label>Follow Y Offset</Label>
        <Slider
          type="range"
          min={-160}
          max={500}
          step={1}
          value={config.followY}
          onChange={(e: Event) =>
            set("followY", Number((e.target as HTMLInputElement).value))
          }
        />
        <SliderValue>{config.followY}</SliderValue>
      </Row>
      <Row>
        <Label>Follow Z Offset</Label>
        <Slider
          type="range"
          min={-500}
          max={500}
          step={1}
          value={config.followZ}
          onChange={(e: Event) =>
            set("followZ", Number((e.target as HTMLInputElement).value))
          }
        />
        <SliderValue>{config.followZ}</SliderValue>
      </Row>

      <Divider />
      <ResetLink onClick={onReset}>Reset Camera Defaults</ResetLink>
    </div>
  );
}

// ── Keyboard Tab ─────────────────────────────────────────────────

function KeyboardTab({
  config,
  capturingAction,
  setCapturingAction,
  onReset,
}: {
  config: SettingsSnapshot;
  capturingAction: string | null;
  setCapturingAction: (action: string | null) => void;
  onReset: () => void;
}) {
  const bindings = config.keyBindings ?? [];

  const handleStartCapture = (action: string) => {
    setCapturingAction(action);
    callNfmw("startCapture", { action });
  };

  return (
    <div>
      <SectionLabel>Key Bindings</SectionLabel>

      <ResetLink onClick={onReset}>Reset All to Defaults</ResetLink>

      <Divider />

      {bindings.map(
        (b) =>
          b && (
            <KeyBindingRow key={b.action}>
              <KeyBindingLabel>{b.displayName || b.action}</KeyBindingLabel>
              <KeyBindingBtn
                capturing={capturingAction === b.action}
                onClick={() => handleStartCapture(b.action)}
              >
                {capturingAction === b.action
                  ? "Press any key..."
                  : keyCodeToName(b.keyCode)}
              </KeyBindingBtn>
            </KeyBindingRow>
          )
      )}
      {capturingAction && (
        <div
          style={{
            fontSize: 12,
            color: "rgba(255,152,0,0.6)",
            marginTop: 10,
            textAlign: "center",
          }}
        >
          Press any key to bind, or ESC to cancel...
        </div>
      )}
    </div>
  );
}
