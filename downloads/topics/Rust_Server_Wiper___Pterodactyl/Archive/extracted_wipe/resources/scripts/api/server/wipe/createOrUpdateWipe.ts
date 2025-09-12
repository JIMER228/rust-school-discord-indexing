import http from '@/api/http';
import { Wipe } from '@/api/server/wipe/getWipeData';

export default (uuid: string, values: Wipe, commandTimes: number[], id?: number): Promise<void> => {
    return new Promise((resolve, reject) => {
        http.post(`/api/client/servers/${uuid}/wipe${id ? '/' + id : ''}`, {
            name: values.name,
            description: values.description,
            size: values.size,
            seed: values.seed,
            random_seed: values.randomSeed,
            random_level: values.randomLevel,
            level: values.level,
            files: values.files,
            commands: values.commands,
            command_times: commandTimes,
            blueprints: values.blueprints,
            schedule: values.schedule,
            time: values.time,
            force: values.force,
            repeat: values.repeat,
        })
            .then(() => resolve())
            .catch(reject);
    });
};
