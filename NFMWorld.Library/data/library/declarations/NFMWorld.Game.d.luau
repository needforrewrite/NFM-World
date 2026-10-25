declare extern type CarStatsData with
    fileName: string
    name: string
    collection: string
    topSpeed: number
    acceleration: number
    handling: number
    powerSave: number
    strength: number
    maxHealth: number
    stunting: number
    hypergliding: number
    abing: number
end

declare CarStatsData: {
}

declare extern type CarCollectionsData with
    collections: { CarCollectionData }
end

declare CarCollectionsData: {
}

declare extern type CarCollectionData with
    id: string
    cars: { CarStatsData }
end

declare CarCollectionData: {
}

declare extern type CurrentCollectionData with
    id: string
end

declare CurrentCollectionData: {
}

declare extern type PauseState with
    lap: integer
    totalLaps: integer
    position: integer
    totalRacers: integer
    stageName: string
end

declare PauseState: {
}

declare extern type AccountData with
    name: string
    isLoggedIn: boolean
    avatarUrl: string
    __eq: ((left: AccountData|nil, right: AccountData|nil) -> boolean)
end

declare AccountData: {
}

declare extern type CapturedKey with
    action: string
    keyCode: integer
    cancelled: boolean
end

declare CapturedKey: {
}

declare extern type AvailableOptions with
    renderers: { string }
    resolutions: { string }
    displayModes: { string }
    antialiasModes: { string }
    shadowCascadeLevels: { string }
    shadowResolutions: { string }
    renderDistanceNames: { string }
end

declare AvailableOptions: {
}

declare extern type CounterData with
    value: integer
end

declare CounterData: {
}

export type SettingsSnapshot = {
    selectedRenderer: integer,
    selectedResolution: integer,
    selectedDisplayMode: integer,
    vsync: boolean,
    fpsLimit: integer,
    antialias: integer,
    shadowCascadeLevel: integer,
    shadowResolution: integer,
    renderDistance: integer,
    lowLatency: boolean,
    lineWidth: number,
    masterVolume: number,
    musicVolume: number,
    effectsVolume: number,
    muteAll: boolean,
    remasteredMusic: boolean,
    fov: number,
    followY: integer,
    followZ: integer,
    smoothFov: boolean,
    keyBindings: {KeyBindingData},
    distantOutlineBehavior: integer,
}


export type KeyBindingData = {
    action: string,
    displayName: string,
    keyCode: integer,
}


