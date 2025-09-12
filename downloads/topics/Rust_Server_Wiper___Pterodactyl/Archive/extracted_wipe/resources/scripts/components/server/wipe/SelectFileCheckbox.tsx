import React from 'react';
import tw from 'twin.macro';
import { ServerContext } from '@/state/server';
import styled from 'styled-components/macro';
import Input from '@/components/elements/Input';

export const FileActionCheckbox = styled(Input)`
    && {
        ${tw`border-neutral-500 bg-transparent`};

        &:not(:checked) {
            ${tw`hover:border-neutral-300`};
        }
    }
`;

export default ({ name }: { name: string }) => {
    const directory = ServerContext.useStoreState((state) => state.files.directory);
    const isChecked = ServerContext.useStoreState(
        (state) => state.files.selectedFiles.indexOf((directory !== '/' ? `${directory}/` : directory) + name) >= 0
    );
    const appendSelectedFile = ServerContext.useStoreActions((actions) => actions.files.appendSelectedFile);
    const removeSelectedFile = ServerContext.useStoreActions((actions) => actions.files.removeSelectedFile);

    return (
        <label css={tw`flex-none px-4 py-2 self-center z-30 cursor-pointer`}>
            <FileActionCheckbox
                name={'selectedFiles'}
                value={name}
                checked={isChecked}
                type={'checkbox'}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) => {
                    if (e.currentTarget.checked) {
                        appendSelectedFile((directory !== '/' ? `${directory}/` : directory) + name);
                    } else {
                        removeSelectedFile((directory !== '/' ? `${directory}/` : directory) + name);
                    }
                }}
            />
        </label>
    );
};
