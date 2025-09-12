import addMap from '@/api/server/wipe/addMap';
import deleteMap from '@/api/server/wipe/deleteMap';
import { Map } from '@/api/server/wipe/getWipeData';
import Button from '@/components/elements/Button';
import Field from '@/components/elements/Field';
import TitledGreyBox from '@/components/elements/TitledGreyBox';
import { Dialog } from '@/components/elements/dialog';
import useFlash from '@/plugins/useFlash';
import { ServerContext } from '@/state/server';
import { Form, Formik, FormikHelpers } from 'formik';
import React, { useState } from 'react';
import tw from 'twin.macro';
import { object, string } from 'yup';

interface Props {
    maps: Map[];
    mutate: () => void;
}

export default ({ maps, mutate }: Props) => {
    const { clearFlashes, clearAndAddHttpError } = useFlash();

    const uuid = ServerContext.useStoreState((state) => state.server.data!.uuid);

    const [mapModal, setMapModal] = useState<boolean>(false);

    const submit = (
        { name, map }: { name: string; map: string },
        { setSubmitting, resetForm }: FormikHelpers<{ name: string; map: string }>
    ) => {
        clearFlashes('server:wipe');

        addMap(uuid, name, map)
            .then(() => {
                mutate();
                resetForm();
            })
            .catch((error) => {
                console.error(error);
                clearAndAddHttpError({ key: 'server:wipe', error });
            })
            .then(() => {
                setSubmitting(false);
                setMapModal(false);
            });
    };

    return (
        <TitledGreyBox title={'Map List'}>
            {maps.length > 0 ? (
                maps.map((map, index) => (
                    <div key={index} css={[tw`flex items-center justify-between mx-4 mt-0`, index > 0 && tw`mt-2`]}>
                        <p title={map.name ?? map.map}>
                            {(map.name ?? map.map).slice(0, 30)}
                            {(map.name ?? map.map).length > 30 && '...'}
                        </p>
                        <div className={'text-right'}>
                            <Button
                                type={'button'}
                                size={'xsmall'}
                                color={'red'}
                                onClick={() => deleteMap(uuid, map.id).then(() => mutate())}
                            >
                                <svg
                                    xmlns='http://www.w3.org/2000/svg'
                                    fill='none'
                                    viewBox='0 0 24 24'
                                    stroke='currentColor'
                                    css={tw`h-5 w-5`}
                                >
                                    <path
                                        strokeLinecap='round'
                                        strokeLinejoin='round'
                                        strokeWidth={2}
                                        d='M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16'
                                    />
                                </svg>
                            </Button>
                        </div>
                    </div>
                ))
            ) : (
                <div css={tw`flex justify-center`}>No maps are set.</div>
            )}
            <div css={tw`mt-5 text-right`}>
                <Button onClick={() => setMapModal(true)}>Add Map</Button>
            </div>
            <Dialog open={mapModal} onClose={() => setMapModal(false)} title={'Add Map'}>
                <Formik
                    onSubmit={submit}
                    initialValues={{ name: '', map: '' }}
                    validationSchema={object().shape({
                        name: string().nullable(),
                        map: string().url().required(),
                    })}
                >
                    {({ submitForm, isSubmitting, isValid }) => (
                        <Form css={tw`m-0`}>
                            <Field
                                type={'string'}
                                name={'name'}
                                label={'Name'}
                                description={
                                    "The name of this map, this is optional and for your own reference only, and It's nowhere used."
                                }
                                disabled={isSubmitting}
                            />
                            <div css={tw`mt-5`}>
                                <Field
                                    type={'string'}
                                    name={'map'}
                                    label={'Custom Map URL'}
                                    description={
                                        'The custom map URL which should be set on a wipe when this map is chosen.'
                                    }
                                    disabled={isSubmitting}
                                />
                            </div>
                            <Dialog.Footer>
                                <Button color='grey' className={'w-full sm:w-auto'} onClick={() => setMapModal(false)}>
                                    Cancel
                                </Button>
                                <Button
                                    className={'w-full sm:w-auto'}
                                    onClick={submitForm}
                                    disabled={isSubmitting || !isValid}
                                >
                                    Add
                                </Button>
                            </Dialog.Footer>
                        </Form>
                    )}
                </Formik>
            </Dialog>
        </TitledGreyBox>
    );
};