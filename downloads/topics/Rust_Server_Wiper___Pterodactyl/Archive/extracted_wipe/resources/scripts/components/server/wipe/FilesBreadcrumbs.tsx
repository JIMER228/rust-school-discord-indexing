import React from 'react';
import { ServerContext } from '@/state/server';
import { NavLink } from 'react-router-dom';
import { encodePathSegments } from '@/helpers';
import tw from 'twin.macro';

export default () => {
    const id = ServerContext.useStoreState((state) => state.server.data!.id);
    const directory = ServerContext.useStoreState((state) => state.files.directory);

    const breadcrumbs = (): { name: string; path?: string }[] =>
        directory
            .split('/')
            .filter((directory) => !!directory)
            .map((directory, index, dirs) => {
                if (index === dirs.length - 1) {
                    return { name: directory };
                }

                return { name: directory, path: `/${dirs.slice(0, index + 1).join('/')}` };
            });

    return (
        <div css={tw`flex flex-grow-0 items-center text-sm text-neutral-500 overflow-x-hidden mb-2`}>
            /<span css={tw`px-1 text-neutral-300`}>home</span>/
            <NavLink to={`/server/${id}/wipe`} css={tw`px-1 text-neutral-200 no-underline hover:text-neutral-100`}>
                container
            </NavLink>
            /
            {breadcrumbs().map((crumb, index) =>
                crumb.path ? (
                    <React.Fragment key={index}>
                        <NavLink
                            to={`/server/${id}/wipe#${encodePathSegments(crumb.path)}`}
                            css={tw`px-1 text-neutral-200 no-underline hover:text-neutral-100`}
                        >
                            {crumb.name}
                        </NavLink>
                        /
                    </React.Fragment>
                ) : (
                    <span key={index} css={tw`px-1 text-neutral-300`}>
                        {crumb.name}
                    </span>
                )
            )}
        </div>
    );
};
