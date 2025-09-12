import React, { useEffect } from 'react';
import tw from 'twin.macro';
import ServerContentBlock from '@/components/elements/ServerContentBlock';
import CreateWipeBox from '@/components/server/wipe/CreateWipeBox';
import TimezoneBox from '@/components/server/wipe/TimezoneBox';
import ScheduledWipesBox from '@/components/server/wipe/ScheduledWipesBox';
import WipeMapsBox from '@/components/server/wipe/WipeMapsBox';
import useSWR from 'swr';
import getWipeData, { WipeData } from '@/api/server/wipe/getWipeData';
import { ServerContext } from '@/state/server';
import Spinner from '@/components/elements/Spinner';
import FlashMessageRender from '@/components/FlashMessageRender';
import useFlash from '@/plugins/useFlash';

export default () => {
    const { clearFlashes, clearAndAddHttpError } = useFlash();

    const uuid = ServerContext.useStoreState((state) => state.server.data!.uuid);

    const { data, error, mutate } = useSWR<WipeData>([uuid, '/wipe'], (key) => getWipeData(key));

    useEffect(() => {
        if (!error) {
            clearFlashes('server:wipe');
            return;
        }

        clearAndAddHttpError({ key: 'server:wipe', error });
    }, [error]);

    return !data ? (
        <Spinner size={'large'} centered />
    ) : (
        <ServerContentBlock title={'Rust Server Wiper'} css={tw`flex flex-wrap`}>
            <div css={tw`w-full`}>
                <FlashMessageRender byKey={'server:wipe'} css={tw`mb-4`} />
            </div>
            <div css={tw`w-full md:w-8/12 pb-4 md:pr-4 md:pb-0`}>
                <CreateWipeBox
                    name={data.name}
                    description={data.description}
                    mutate={mutate}
                    timezones={data.timezones}
                />
            </div>
            <div css={tw`w-full md:w-4/12`}>
                <TimezoneBox timezones={data.timezones} />
                <div css={tw`pt-4`}>
                    <ScheduledWipesBox wipes={data.wipes} mutate={mutate} timezones={data.timezones} />
                </div>
                <div css={tw`pt-4`}>
                    <WipeMapsBox maps={data.maps} mutate={mutate} />
                </div>
            </div>
        </ServerContentBlock>
    );
};
