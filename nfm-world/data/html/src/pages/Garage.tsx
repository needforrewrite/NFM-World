import { useState, useEffect, useRef } from "preact/hooks";
import { callNfmw, onNfmwEvent } from "@shared/bridge";
import { GlassCard, StatBar } from "@shared/components/GlassCard";
import { CarStatsData } from "@shared/memorypack/CarStatsData";
import { CarCollectionsData } from "@shared/memorypack/CarCollectionsData";
import { CurrentCollectionData } from "@shared/memorypack/CurrentCollectionData";

// ── Garage ───────────────────────────────────────────────────────
// Functional Preact component: car selection + stat display + search + collection switching.

export function Garage() {
  const [currentCar, setCurrentCar] = useState<CarStatsData | null>(null);
  const [collections, setCollections] = useState<CarCollectionsData | null>(null);
  const [currentCollection, setCurrentCollection] = useState<CurrentCollectionData | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const searchInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const u1 = onNfmwEvent<CarStatsData | null>("garage:currentCar", setCurrentCar, CarStatsData.deserialize.bind(CarStatsData));
    const u2 = onNfmwEvent<CarCollectionsData | null>("garage:collections", setCollections, CarCollectionsData.deserialize.bind(CarCollectionsData));
    const u3 = onNfmwEvent<CurrentCollectionData | null>("garage:currentCollection", setCurrentCollection, CurrentCollectionData.deserialize.bind(CurrentCollectionData));
    return () => { u1(); u2(); u3(); };
  }, []);

  // ── Keyboard shortcuts ──────────────────────────────────────
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Don't intercept keys when search input is focused.
      if (document.activeElement === searchInputRef.current) return;

      switch (e.key) {
        case "ArrowLeft":
          callNfmw("cycleCar", { direction: "left" });
          break;
        case "ArrowRight":
          callNfmw("cycleCar", { direction: "right" });
          break;
        case "Enter":
          callNfmw("confirm");
          break;
        case "Escape":
          callNfmw("cancel");
          break;
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, []);

  const handleBack = () => callNfmw("back");
  const handleSelectCar = (collection: string, carName: string) => {
    callNfmw("selectCar", { collection, carName });
  };
  const handleSelectCollection = (collection: string) => {
    callNfmw("selectCollection", { collection });
  };

  // ── Client-side search filter ──────────────────────────────
  const query = searchQuery.toLowerCase().trim();
  const filteredCollections = !query
    ? collections?.collections
    : collections?.collections?.map((col) => {
        if (!col) return null;
        const filteredCars = col.cars?.filter((car) =>
          car != null && car.name.toLowerCase().includes(query)
        );
        if (!filteredCars || filteredCars.length === 0) return null;
        return { ...col, cars: filteredCars };
      }).filter(Boolean);

  return (
    <div style={{ width: "100%", height: "100%", display: "flex", animation: "nfmw-fadeIn 0.3s ease-out" }}>
      <div style={{
        width: "340px", height: "100%", padding: "32px 24px",
        display: "flex", flexDirection: "column", gap: "16px",
        background: "rgba(0,0,0,0.3)", borderRight: "1px solid rgba(255,255,255,0.06)",
        overflowY: "auto",
      }}>
        <div style={{ fontSize: "28px", fontWeight: 700, letterSpacing: "2px", textTransform: "uppercase" }}>
          Garage
        </div>

        {/* ── Search input ─────────────────────────────────── */}
        <input
          ref={searchInputRef}
          type="text"
          placeholder="Search cars..."
          value={searchQuery}
          onInput={(e) => setSearchQuery((e.target as HTMLInputElement).value)}
          onKeyDown={(e) => {
            // Enter in search input selects first match.
            if (e.key === "Enter" && filteredCollections && filteredCollections.length > 0) {
              const firstCol = filteredCollections[0];
              if (firstCol && firstCol.cars && firstCol.cars.length > 0 && firstCol.cars[0]) {
                handleSelectCar(firstCol.name, firstCol.cars[0].name);
                setSearchQuery("");
                (e.target as HTMLInputElement).blur();
              }
            }
            // Escape clears search.
            if (e.key === "Escape") {
              setSearchQuery("");
              (e.target as HTMLInputElement).blur();
            }
          }}
          style={{
            width: "100%", padding: "10px 14px", fontSize: "14px",
            color: "#fff", background: "rgba(255,255,255,0.08)",
            border: "1px solid rgba(255,255,255,0.12)", borderRadius: "8px",
            outline: "none",
          }}
        />

        {/* ── Current car stats ─────────────────────────────── */}
        <div style={{ flex: 1, display: "flex", alignItems: "center", justifyContent: "center", color: "rgba(255,255,255,0.3)", fontSize: "18px", letterSpacing: "2px" }}>
            {currentCar ? (
              <div style={{ width: "280px", padding: "24px" }}>
                  <div style={{ fontSize: "18px", fontWeight: 600, color: "#4fc3f7", marginBottom: "16px" }}>
                  {currentCar.name}
                  </div>
                  <StatBar label="Top Speed" value={currentCar.topSpeed} color="#ff6e40" />
                  <StatBar label="Acceleration" value={currentCar.acceleration} color="#ffd740" />
                  <StatBar label="Handling" value={currentCar.handling} color="#69f0ae" />
                  <StatBar label="Power Save" value={currentCar.powerSave} color="#40c4ff" />
                  <StatBar label="Strength" value={currentCar.strength} color="#ff4081" />
                  <StatBar label="Max Health" value={currentCar.maxHealth} color="#e040fb" />
                  <StatBar label="Stunting" value={currentCar.stunting} color="#ff6e40" />
                  <StatBar label="Hypergliding" value={currentCar.hypergliding} color="#7c4dff" />
                  <StatBar label="AB'ing" value={currentCar.abing} color="#448aff" />
              </div>
            ) : (
              "Select a car to view stats"
            )}
        </div>

        {/* ── Collection & car list ─────────────────────────── */}
        {filteredCollections?.map((col) => (
          col != null &&
            <div key={col.name}>
              <div
                onClick={() => handleSelectCollection(col.name)}
                style={{
                  fontSize: "12px", marginBottom: "6px", letterSpacing: "1px", textTransform: "uppercase",
                  cursor: "pointer", padding: "4px 8px", borderRadius: "4px",
                  color: currentCollection?.id === col.id ? "#4fc3f7" : "rgba(255,255,255,0.4)",
                  background: currentCollection?.id === col.id ? "rgba(79,195,247,0.08)" : "transparent",
                  borderLeft: currentCollection?.id === col.id ? "2px solid #4fc3f7" : "2px solid transparent",
                  transition: "color 0.15s ease, background 0.15s ease",
                }}
              >
                {col.name}
              </div>
              {col.cars?.map((car) => (
                car != null &&
                  <GlassCard
                    key={car.name}
                    color={currentCar?.name === car.name ? "#4fc3f7" : "rgba(255,255,255,0.15)"}
                    style={{
                      marginBottom: "8px", cursor: "pointer",
                      opacity: currentCar?.name === car.name ? 1 : 0.7,
                      transition: "opacity 0.15s ease",
                    }}
                  >
                    <div onClick={() => handleSelectCar(col.name, car.name)}>
                      <div style={{ fontWeight: 600, fontSize: "14px", marginBottom: "4px" }}>{car.name}</div>
                      <div style={{ fontSize: "11px", color: "rgba(255,255,255,0.4)" }}>{col.name}</div>
                    </div>
                  </GlassCard>
              ))}
            </div>
        ))}

        {/* ── No results ────────────────────────────────────── */}
        {query && filteredCollections?.length === 0 && (
          <div style={{ color: "rgba(255,255,255,0.3)", fontSize: "14px", textAlign: "center", padding: "24px" }}>
            No cars match "{searchQuery}"
          </div>
        )}

        <button
          onClick={handleBack}
          style={{
            padding: "10px 24px", fontSize: "14px", fontWeight: 600,
            color: "rgba(255,255,255,0.6)", background: "rgba(255,255,255,0.06)",
            border: "1px solid rgba(255,255,255,0.1)", borderRadius: "6px",
            cursor: "pointer", marginTop: "auto",
          }}
        >
          ← Back to Menu
        </button>
      </div>
    </div>
  );
}
