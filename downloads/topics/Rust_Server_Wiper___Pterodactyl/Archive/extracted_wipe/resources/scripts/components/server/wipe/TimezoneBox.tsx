import React from 'react';
import TitledGreyBox from '@/components/elements/TitledGreyBox';
import tw from 'twin.macro';
import { ServerContext } from '@/state/server';
import Select from '@/components/elements/Select';
import setTimezone from '@/api/server/wipe/setTimezone';

interface Props {
    timezones: string[];
}

export default ({ timezones }: Props) => {
    const server = ServerContext.useStoreState((state) => state.server.data!);
    const uuid = ServerContext.useStoreState((state) => state.server.data!.uuid);
    const timezone = ServerContext.useStoreState((state) => state.server.data!.timezone);
    const setServer = ServerContext.useStoreActions((actions) => actions.server.setServer);

    const curTime = new Date().toLocaleString('en-US', { timeZone: timezone ?? timezones[0] });

    return (
        <TitledGreyBox title={'Timezone'}>
            This timezone will be used for all your scheduled wipes.
            <Select
                onChange={(e) => {
                    setServer({ ...server, timezone: e.currentTarget.value });
                    setTimezone(uuid, e.currentTarget.value);
                }}
                defaultValue={timezone}
                css={tw`my-5`}
            >
                {timezones.map((timezone, index) => (
                    <option key={index} value={timezone}>
                        {timezone}
                    </option>
                ))}
            </Select>
            Current time: <b>{curTime}</b>
        </TitledGreyBox>
    );
};
