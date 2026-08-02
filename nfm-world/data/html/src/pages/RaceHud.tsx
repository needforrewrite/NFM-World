import { useState, useEffect, useRef, useMemo } from "preact/hooks";
import { onNfmwEvent } from "../shared/bridge";
import { HudStateData } from "../shared/memorypack/HudStateData";
import { GlassCard, StatBar, CenterText } from "../shared/components/GlassCard";
import { PauseMenu } from "./PauseMenu";

// ── RaceHUD ──────────────────────────────────────────────────────
// Functional Preact component: in-race HUD overlay.

function formatTime(ms: number): string {
  const timeMins = Math.floor(ms / (1000 * 60));
  const timeMinsFmt = `${timeMins.toString().padStart(2, "0")}`;
  const timeSecs = Math.floor(ms / 1000 % 60);
  const timeMs = ms % 1000;
  const fmt = `${(timeMins > 0 ? timeMinsFmt + ":" : "")}${timeSecs.toString().padStart(2, "0")}.${timeMs.toString().padStart(3, "0")}`;
  return fmt;
}

function shallowEqual(a: unknown[] | null, b: unknown[] | null): boolean {
  if (a === b) return true;
  if (a == null) return b == null;
  if (b == null) return false;
  if (a.length !== b.length) return false;
  return a.every((el, i) => el === b[i]);
}

export function RaceHud() {
  const [speed, setSpeed] = useState(0);
  const [power, setPower] = useState(1);
  const [damage, setDamage] = useState(0);
  const [lap, setLap] = useState(1);
  const [totalLaps, setTotalLaps] = useState(3);
  const [position, setPosition] = useState(1);
  const [totalRacers, setTotalRacers] = useState(1);
  const [stateText, setStateText] = useState<string | null>(null);
  const [stateTextEndsAt, setStateTextEndsAt] = useState<Date | null>(null);
  const [chkDiffMs, setChkDiffMs] = useState<number | null>(null);
  const [lastChkDiffMs, setLastChkDiffMs] = useState<number | null>(null);
  const [lapDiffMs, setLapDiffMs] = useState<number | null>(null);
  const [lastLapDiffMs, setLastLapDiffMs] = useState<number | null>(null);
  const [lapTime, setLapTime] = useState(0);
  const [countdownTimer, setCountdownTimer] = useState<number>(0);
  const [isPaused, setIsPaused] = useState(false);

  useEffect(() => {
    return onNfmwEvent<HudStateData | null>("race:hudState", (newHud) => {
      if (newHud == null) return;

      setSpeed(newHud.speed);
      setPower(newHud.power);
      setDamage(newHud.damage);
      setLap(newHud.lap);
      setTotalLaps(newHud.totalLaps);
      setPosition(newHud.position);
      setTotalRacers(newHud.totalRacers);
      setStateText(newHud.stateText);
      setStateTextEndsAt(newHud.stateTextEndsAt ?? null);
      setChkDiffMs(newHud.chkDiffMs ?? null);
      setLastChkDiffMs(newHud.lastChkDiffMs ?? null);
      setLapDiffMs(newHud.lapDiffMs ?? null);
      setLastLapDiffMs(newHud.lastLapDiffMs ?? null);
      setLapTime(newHud.lapTime ?? 0);
      setCountdownTimer(newHud.countdownTimer);
    }, HudStateData.deserialize.bind(HudStateData));
  }, []);

  // Listen for pause/resume events from C#
  useEffect(() => {
    return onNfmwEvent<boolean>("race:paused", (paused) => {
      setIsPaused(paused);
    });
  }, []);

  useEffect(() => {
    if (stateTextEndsAt != null && stateTextEndsAt.getTime() - Date.now() > 0) {
      const timer = setTimeout(() => {
        setStateText(null);
      }, stateTextEndsAt ? stateTextEndsAt.getTime() - Date.now() : 0);
      return () => clearTimeout(timer);
    }
  }, [stateTextEndsAt]);

  const actualStateText = useMemo(() => {
    if (countdownTimer > 0) {
      return `Starting in ${countdownTimer}`;
    }
    return stateText ?? "";
  }, [stateText, countdownTimer]);

  const speedKmh = useMemo(() => speed * 1.4 * 21.0 * 60.0 * 60.0 / 100000.0, [speed]);
  const speedMph = useMemo(() => speedKmh * 0.621371, [speedKmh]);

  return (
    <div style={{ width: "100%", height: "100%", position: "relative" }}>
      <CenterText
        text={actualStateText}
        size={24}
      />

      {/* Power & Damage (top-right) */}
      <div
        style={{
          position: "absolute",
          top: "16px",
          right: "16px",
          width: "200px",
          display: "flex",
          flexDirection: "column",
          gap: "8px",
          animation: "nfmw-fadeIn 0.3s ease-out",
        }}
      >
        <GlassCard color="#ff5252">
          <div
            style={{
              fontSize: "11px",
              color: "rgba(255,255,255,0.5)",
              marginBottom: "4px",
              letterSpacing: "1px",
              textTransform: "uppercase",
            }}
          >
            Power
          </div>
          <StatBar
            label=""
            value={power / 0.98}
            color="#ff5252"
            height={10}
          />
        </GlassCard>
        <GlassCard color="#4fc3f7">
          <div
            style={{
              fontSize: "11px",
              color: "rgba(255,255,255,0.5)",
              marginBottom: "4px",
              letterSpacing: "1px",
              textTransform: "uppercase",
            }}
          >
            Damage
          </div>
          <StatBar label="" value={damage} color="#4fc3f7" height={10} />
        </GlassCard>
      </div>

      {/* Lap timer & splits (top-left) */}
      <div
        style={{
          position: "absolute",
          top: "16px",
          left: "16px",
          width: "180px",
          animation: "nfmw-fadeIn 0.3s ease-out",
        }}
      >
        <GlassCard color="#69f0ae">
          <div
            style={{
              fontSize: "11px",
              color: "rgba(255,255,255,0.5)",
              marginBottom: "4px",
              letterSpacing: "1px",
              textTransform: "uppercase",
            }}
          >
            Lap {lap}/{totalLaps}
          </div>
          <div
            style={{
              fontSize: "28px",
              fontWeight: 700,
              fontVariantNumeric: "tabular-nums",
            }}
          >
            {formatTime(lapTime)}
          </div>
          {chkDiffMs && lastChkDiffMs && (
            <div
              style={{
                fontSize: "11px",
                color: "rgba(255,255,255,0.4)",
                marginTop: "4px",
              }}
            >
              CHK Diff: {formatTime(chkDiffMs)} (Last: {formatTime(lastChkDiffMs)})
            </div>
          )}
          {lapDiffMs && lastLapDiffMs && (
            <div
              style={{
                fontSize: "11px",
                color: "rgba(255,255,255,0.4)",
                marginTop: "4px",
              }}
            >
              Lap Diff: {formatTime(lapDiffMs)} (Last: {formatTime(lastLapDiffMs)})
            </div>
          )}
        </GlassCard>
      </div>

      {/* Speed (bottom-center) */}
      <div
        style={{
          position: "absolute",
          bottom: "24px",
          left: "50%",
          transform: "translateX(-50%)",
          textAlign: "center",
          animation: "nfmw-fadeIn 0.3s ease-out",
        }}
      >
        <div
          style={{
            fontSize: "56px",
            fontWeight: 800,
            letterSpacing: "-2px",
            lineHeight: 1,
            textShadow: "0 2px 16px rgba(0,0,0,0.5)",
          }}
        >
          {Math.round(speedKmh)}
        </div>
        <div
          style={{
            fontSize: "14px",
            color: "rgba(255,255,255,0.5)",
            letterSpacing: "2px",
          }}
        >
          KM/H
        </div>
      </div>

      {/* Position (bottom-right) */}
      <div
        style={{
          position: "absolute",
          bottom: "24px",
          right: "24px",
          textAlign: "right",
          animation: "nfmw-fadeIn 0.3s ease-out",
        }}
      >
        <div style={{ fontSize: "36px", fontWeight: 700 }}>
          {position}
          <span style={{ fontSize: "16px", color: "rgba(255,255,255,0.4)" }}>
            /{totalRacers}
          </span>
        </div>
        <div
          style={{
            fontSize: "12px",
            color: "rgba(255,255,255,0.5)",
            letterSpacing: "1px",
          }}
        >
          POSITION
        </div>
      </div>

      {/* Pause menu overlay — shown/hidden via C# "race:paused" event */}
      <PauseMenu visible={isPaused} />
    </div>
  );
}
