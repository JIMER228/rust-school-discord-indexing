import http, { FractalResponseData, FractalResponseList } from '@/api/http';

interface WipeCommand {
    command: string;
    time: string;
}

export interface Wipe {
    id?: number;
    name: string;
    description: string;
    size: string;
    seed: string;
    randomSeed: boolean;
    randomLevel: boolean;
    level: string;
    files: string;
    commands: string[];
    blueprints: boolean;
    schedule: boolean;
    time: Date | null;
    force: boolean;
    repeat: boolean;

    relationships?: {
        commands: WipeCommand[];
    };
}

export interface Map {
    id: number;
    name: string;
    map: string;
}

export interface WipeData {
    name: string;
    description: string;
    timezones: string[];
    maps: Map[];
    wipes: Wipe[];
}

const rawDataToServerWipeCommand = ({ attributes }: FractalResponseData): WipeCommand => ({
    command: attributes.command,
    time: attributes.time,
});

const rawDataToServerWipe = ({ attributes }: FractalResponseData): Wipe => ({
    id: attributes.id,
    name: attributes.name,
    description: attributes.description,
    size: attributes.size,
    seed: attributes.seed,
    randomSeed: attributes.random_seed,
    randomLevel: attributes.random_level,
    level: attributes.level,
    files: attributes.files,
    commands: attributes.commands,
    blueprints: attributes.blueprints,
    schedule: attributes.schedule,
    time: attributes.time,
    force: attributes.force,
    repeat: attributes.repeat,
    relationships: {
        commands: ((attributes.relationships?.commands as FractalResponseList | undefined)?.data || []).map(
            rawDataToServerWipeCommand
        ),
    },
});

export default async (uuid: string): Promise<WipeData> => {
    const { data } = await http.get(`/api/client/servers/${uuid}/wipe`);
    return {
        name: data.name,
        description: data.description,
        timezones: data.timezones,
        maps: data.maps,
        wipes: (data.wipes.data || []).map(rawDataToServerWipe),
    };
};
