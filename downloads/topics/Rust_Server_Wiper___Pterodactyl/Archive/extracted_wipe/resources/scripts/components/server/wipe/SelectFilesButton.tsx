import React, { useState, useEffect } from 'react';
import tw from 'twin.macro';
import SpinnerOverlay from '@/components/elements/SpinnerOverlay';
import Button from '@/components/elements/Button';
import { ServerContext } from '@/state/server';
import Modal from '@/components/elements/Modal';
import { useLocation, useHistory } from 'react-router-dom';
import useFileManagerSwr from '@/plugins/useFileManagerSwr';
import { FileObject } from '@/api/server/files/loadDirectory';
import { hashToPath } from '@/helpers';
import FileRow from '@/components/server/wipe/FileRow';
import { useFormikContext } from 'formik';
import FilesBreadcrumbs from '@/components/server/wipe/FilesBreadcrumbs';
import { Wipe } from '@/api/server/wipe/getWipeData';

const sortFiles = (files: FileObject[]): FileObject[] => {
    const sortedFiles: FileObject[] = files
        .sort((a, b) => a.name.localeCompare(b.name))
        .sort((a, b) => (a.isFile === b.isFile ? 0 : a.isFile ? 1 : -1));
    return sortedFiles.filter((file, index) => index === 0 || file.name !== sortedFiles[index - 1].name);
};

export default () => {
    const id = ServerContext.useStoreState((state) => state.server.data!.id);
    const directory = ServerContext.useStoreState((state) => state.files.directory);
    const setDirectory = ServerContext.useStoreActions((actions) => actions.files.setDirectory);

    const selectedFiles = ServerContext.useStoreState((state) => state.files.selectedFiles);
    const setSelectedFiles = ServerContext.useStoreActions((actions) => actions.files.setSelectedFiles);

    const [visible, setVisible] = useState(false);

    const history = useHistory();
    const { hash } = useLocation();
    const { data: files, mutate } = useFileManagerSwr();

    const { setFieldValue, values } = useFormikContext<Wipe>();

    const setFiles = () => {
        setFieldValue('files', values.files + `${values.files ? '\n' : ''}${selectedFiles.join('\n')}`);
        setSelectedFiles([]);
        setVisible(false);
        history.push(`/server/${id}/wipe`);
    };

    useEffect(() => {
        setSelectedFiles([]);
        setDirectory(hashToPath(hash));
    }, [hash]);

    useEffect(() => {
        mutate();
    }, [directory]);

    return (
        <>
            <Button
                css={tw`ml-2 w-1/5 my-auto whitespace-nowrap`}
                type={'button'}
                size={'large'}
                onClick={() => setVisible(true)}
            >
                Select Files
            </Button>
            <Modal
                visible={visible}
                onDismissed={() => {
                    setVisible(false);
                    history.push(`/server/${id}/wipe`);
                }}
            >
                <FilesBreadcrumbs />
                {!files ? (
                    <SpinnerOverlay visible />
                ) : (
                    sortFiles(files.slice(0, 250)).map((file) => <FileRow key={file.key} file={file} />)
                )}
                <div css={tw`flex justify-end`}>
                    <Button css={tw`mt-8`} onClick={setFiles}>
                        Select Files
                    </Button>
                </div>
            </Modal>
        </>
    );
};
