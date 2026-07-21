import { MemoryPackWriter } from "./MemoryPackWriter";
import { MemoryPackReader } from "./MemoryPackReader";
import { Collection } from "./Collection";

export class CarStatsData {
    name: string;
    collection: Collection;
    topSpeed: number;
    acceleration: number;
    handling: number;
    powerSave: number;
    strength: number;
    maxHealth: number;
    stunting: number;
    hypergliding: number;
    abing: number;

    constructor() {
        this.name = "";
        this.collection = 0;
        this.topSpeed = 0;
        this.acceleration = 0;
        this.handling = 0;
        this.powerSave = 0;
        this.strength = 0;
        this.maxHealth = 0;
        this.stunting = 0;
        this.hypergliding = 0;
        this.abing = 0;

    }

    static serialize(value: CarStatsData | null): Uint8Array {
        const writer = MemoryPackWriter.getSharedInstance();
        this.serializeCore(writer, value);
        return writer.toArray();
    }

    static serializeCore(writer: MemoryPackWriter, value: CarStatsData | null): void {
        if (value == null) {
            writer.writeNullObjectHeader();
            return;
        }

        writer.writeObjectHeader(11);
        writer.writeString(value.name);
        writer.writeInt32(value.collection);
        writer.writeFloat64(value.topSpeed);
        writer.writeFloat64(value.acceleration);
        writer.writeFloat64(value.handling);
        writer.writeFloat64(value.powerSave);
        writer.writeFloat64(value.strength);
        writer.writeFloat64(value.maxHealth);
        writer.writeFloat64(value.stunting);
        writer.writeFloat64(value.hypergliding);
        writer.writeFloat64(value.abing);

    }

    static serializeArray(value: (CarStatsData | null)[] | null): Uint8Array {
        const writer = MemoryPackWriter.getSharedInstance();
        this.serializeArrayCore(writer, value);
        return writer.toArray();
    }

    static serializeArrayCore(writer: MemoryPackWriter, value: (CarStatsData | null)[] | null): void {
        writer.writeArray(value, (writer, x) => CarStatsData.serializeCore(writer, x));
    }

    static deserialize(buffer: ArrayBuffer): CarStatsData | null {
        return this.deserializeCore(new MemoryPackReader(buffer));
    }

    static deserializeCore(reader: MemoryPackReader): CarStatsData | null {
        const [ok, count] = reader.tryReadObjectHeader();
        if (!ok) {
            return null;
        }

        const value = new CarStatsData();
        if (count == 11) {
            value.name = reader.readString()!;
            value.collection = reader.readInt32()!;
            value.topSpeed = reader.readFloat64()!;
            value.acceleration = reader.readFloat64()!;
            value.handling = reader.readFloat64()!;
            value.powerSave = reader.readFloat64()!;
            value.strength = reader.readFloat64()!;
            value.maxHealth = reader.readFloat64()!;
            value.stunting = reader.readFloat64()!;
            value.hypergliding = reader.readFloat64()!;
            value.abing = reader.readFloat64()!;

        }
        else if (count > 11) {
            throw new Error("Current object's property count is larger than type schema, can't deserialize about versioning.");
        }
        else {
            if (count == 0) return value;
            value.name = reader.readString()!; if (count == 1) return value;
            value.collection = reader.readInt32()!; if (count == 2) return value;
            value.topSpeed = reader.readFloat64()!; if (count == 3) return value;
            value.acceleration = reader.readFloat64()!; if (count == 4) return value;
            value.handling = reader.readFloat64()!; if (count == 5) return value;
            value.powerSave = reader.readFloat64()!; if (count == 6) return value;
            value.strength = reader.readFloat64()!; if (count == 7) return value;
            value.maxHealth = reader.readFloat64()!; if (count == 8) return value;
            value.stunting = reader.readFloat64()!; if (count == 9) return value;
            value.hypergliding = reader.readFloat64()!; if (count == 10) return value;
            value.abing = reader.readFloat64()!; if (count == 11) return value;

        }
        return value;
    }

    static deserializeArray(buffer: ArrayBuffer): (CarStatsData | null)[] | null {
        return this.deserializeArrayCore(new MemoryPackReader(buffer));
    }

    static deserializeArrayCore(reader: MemoryPackReader): (CarStatsData | null)[] | null {
        return reader.readArray(reader => CarStatsData.deserializeCore(reader));
    }
}
