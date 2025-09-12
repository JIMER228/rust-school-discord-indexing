import React, { useState } from 'react';
import TitledGreyBox from '@/components/elements/TitledGreyBox';
import tw from 'twin.macro';
import { Form, Formik, FormikHelpers } from 'formik';
import Field, { TextareaField } from '@/components/elements/Field';
import Label from '@/components/elements/Label';
import Button from '@/components/elements/Button';
import FormikSwitch from '@/components/elements/FormikSwitch';
import { ServerContext } from '@/state/server';
import useFlash from '@/plugins/useFlash';
import createOrUpdateWipe from '@/api/server/wipe/createOrUpdateWipe';
import { Wipe } from '@/api/server/wipe/getWipeData';
import { object, string, number, boolean, lazy, date } from 'yup';
import SelectFilesButton from '@/components/server/wipe/SelectFilesButton';
import Select from '@/components/elements/Select';

interface Props {
    name: string;
    description: string;
    mutate: () => void;
    timezones: string[];
}

export const initialValues: Wipe = {
    name: '',
    description: '',
    size: '',
    seed: '',
    randomSeed: false,
    randomLevel: false,
    level: '',
    files: '',
    commands: [''],
    blueprints: false,
    schedule: false,
    time: null,
    force: false,
    repeat: false,
};

export default ({ name, description, mutate, timezones }: Props) => {
    const { clearFlashes, clearAndAddHttpError } = useFlash();

    const uuid = ServerContext.useStoreState((state) => state.server.data!.uuid);
    const timezone = ServerContext.useStoreState((state) => state.server.data!.timezone);

    const [commandTimes, setCommandTimes] = useState([1]);

    const times: number[] = [];
    for (let i = 1; i <= 60; i++) {
        times.push(i);
    }

    const submit = (values: Wipe, { setSubmitting, resetForm }: FormikHelpers<Wipe>) => {
        clearFlashes('server:wipe');

        createOrUpdateWipe(uuid, values, commandTimes)
            .then(() => {
                mutate();
                resetForm();
                setCommandTimes([1]);
            })
            .catch((error) => {
                console.error(error);
                clearAndAddHttpError({ key: 'server:wipe', error });
            })
            .then(() => setSubmitting(false));
    };

    return (
        <Formik
            onSubmit={submit}
            initialValues={initialValues}
            validationSchema={object().shape({
                name: string().required().max(191),
                description: string().required(),
                size: number().when(['level', 'randomLevel'], {
                    is: (a, b) => a === undefined && b === false,
                    then: number().required(),
                }),
                randomSeed: boolean(),
                randomLevel: boolean(),
                seed: lazy(() =>
                    number().when(['level', 'randomSeed', 'randomLevel'], {
                        is: (a, b, c) => a === undefined && b === false && c === false,
                        then: number().required(),
                    })
                ),
                level: lazy(() =>
                    string().when(['seed', 'randomSeed', 'randomLevel'], {
                        is: (a, b, c) => a === undefined && b === false && c === false,
                        then: string().required(),
                    })
                ),
                files: string(),
                blueprints: boolean(),
                schedule: boolean(),
                time: date()
                    .nullable()
                    .when(['schedule', 'force'], {
                        is: (schedule, force) => schedule === true && force !== true,
                        then: date()
                            .required()
                            .min(new Date().toLocaleString('en', { timeZone: timezone ?? timezones[0] })),
                    }),
                force: boolean(),
                repeat: boolean(),
            })}
        >
            {({ isSubmitting, values, setFieldValue }) => (
                <TitledGreyBox title={'Create Wipe'}>
                    <Form css={tw`m-0`}>
                        <div css={tw`flex`}>
                            <div css={tw`w-3/5`}>
                                <Field
                                    type={'string'}
                                    name={'name'}
                                    label={'Server Name'}
                                    description={
                                        'The server name which should be set on this wipe, %DAY% and %MONTH% will be replaced with the day and month on which the wipe is taking place.'
                                    }
                                    disabled={isSubmitting}
                                />
                            </div>
                            <Button
                                css={tw`ml-2 w-2/5 my-auto whitespace-nowrap`}
                                type={'button'}
                                color={'grey'}
                                size={'large'}
                                onClick={() => setFieldValue('name', name)}
                                disabled={isSubmitting}
                            >
                                Load Current
                            </Button>
                        </div>
                        <div css={tw`flex mt-5`}>
                            <div css={tw`w-3/5`}>
                                <Field
                                    type={'string'}
                                    name={'description'}
                                    label={'Description'}
                                    description={
                                        'The server description which should be set on this wipe, %DAY% and %MONTH% will be replaced with the day and month on which the wipe is taking place.'
                                    }
                                    disabled={isSubmitting}
                                />
                            </div>
                            <Button
                                css={tw`ml-2 w-2/5 my-auto`}
                                type={'button'}
                                color={'grey'}
                                size={'large'}
                                onClick={() => setFieldValue('description', description)}
                                disabled={isSubmitting}
                            >
                                Load Current
                            </Button>
                        </div>
                        <div css={tw`grid grid-cols-2 gap-4 mt-5`}>
                            <Field
                                type={'string'}
                                name={'size'}
                                label={'Map Size'}
                                description={
                                    'The map size which should be set on this wipe. This field is not required when using a custom map.'
                                }
                                disabled={!!values.level || values.randomLevel || isSubmitting}
                            />
                            <Field
                                type={'string'}
                                name={'seed'}
                                label={'Map Seed'}
                                description={
                                    'The seed which should be used on this wipe. This field is not required when using a custom map.'
                                }
                                disabled={values.randomSeed || !!values.level || values.randomLevel || isSubmitting}
                            />
                        </div>
                        <div css={tw`grid grid-cols-2 gap-4 mt-5`}>
                            <FormikSwitch
                                name={'randomSeed'}
                                description={'Generate a random seed on this wipe.'}
                                label={'Random Seed'}
                                checked={values.randomSeed}
                                readOnly={!!values.seed || !!values.level || values.randomLevel || isSubmitting}
                            />
                            <FormikSwitch
                                name={'randomLevel'}
                                description={'Use a random level from the map list.'}
                                label={'Random Level'}
                                checked={values.randomLevel}
                                readOnly={values.randomSeed || !!values.seed || !!values.level || isSubmitting}
                            />
                        </div>
                        <div css={tw`mt-5`}>
                            <Field
                                type={'string'}
                                name={'level'}
                                label={'Custom Map URL'}
                                description={
                                    'The custom map URL which should be set on this wipe. This field is not required when using a seed.'
                                }
                                disabled={values.randomSeed || !!values.seed || values.randomLevel || isSubmitting}
                            />
                        </div>
                        <div css={tw`flex mt-5`}>
                            <div css={tw`w-4/5`}>
                                <TextareaField
                                    name={'files'}
                                    label={'Files'}
                                    description={
                                        'All the files and directories which should be removed on this wipe. You can use * as a wildcard, like: /server/rust/*.map to delete all .map files in the /server/rust directory. Keep in mind that using wildcards will extend the time it takes to wipe your server. NOTE: nothing is deleted by itself, if you want to wipe your map, you will need to specify your map files here.'
                                    }
                                    rows={5}
                                    disabled={isSubmitting}
                                />
                            </div>
                            <SelectFilesButton />
                        </div>
                        <div css={tw`mt-5`}>
                            <FormikSwitch
                                name={'blueprints'}
                                label={'Wipe Blueprints'}
                                description={'Wipe all the blueprints on this wipe.'}
                                checked={values.blueprints}
                                readOnly={isSubmitting}
                            />
                        </div>
                        <div css={tw`mt-5`}>
                            <FormikSwitch
                                name={'schedule'}
                                label={'Schedule Wipe'}
                                description={'Enable this to schedule this wipe on a specific date and time.'}
                                checked={values.schedule}
                                readOnly={isSubmitting}
                            />
                        </div>
                        {values.schedule && (
                            <>
                                <div css={tw`mt-5`}>
                                    <Field
                                        type={'datetime-local'}
                                        name={'time'}
                                        label={'Schedule Time'}
                                        description={'The exact time on which this wipe should happen.'}
                                        disabled={isSubmitting || (values.force && !values.repeat)}
                                    />
                                </div>
                                <div css={tw`mt-5`}>
                                    <Label>Commands</Label>
                                    <div css={tw`grid grid-cols-4 gap-3`}>
                                        {commandTimes.map((selected, index) => (
                                            <>
                                                <div css={tw`col-span-2`}>
                                                    <Field
                                                        type={'string'}
                                                        name={`commands.${index}`}
                                                        disabled={isSubmitting}
                                                    />
                                                </div>
                                                <Select
                                                    onChange={(e) => {
                                                        const array = commandTimes;
                                                        array[index] = Number(e.currentTarget.value);
                                                        setCommandTimes(array);
                                                    }}
                                                    disabled={isSubmitting}
                                                >
                                                    {times.map((time) => (
                                                        <option key={time} value={time} selected={time === selected}>
                                                            {time} minutes before wipe
                                                        </option>
                                                    ))}
                                                </Select>
                                                <div css={tw`flex justify-evenly`}>
                                                    {commandTimes.length !== 1 && (
                                                        <Button
                                                            type={'button'}
                                                            size={'small'}
                                                            color={'red'}
                                                            onClick={() => {
                                                                setCommandTimes(
                                                                    commandTimes.filter(
                                                                        (item, itemIndex) => itemIndex !== index
                                                                    )
                                                                );
                                                                setFieldValue(`commands.${index}`, '');
                                                            }}
                                                            disabled={isSubmitting}
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
                                                    )}
                                                    {index + 1 === commandTimes.length && (
                                                        <Button
                                                            type={'button'}
                                                            size={'small'}
                                                            color={'green'}
                                                            onClick={() => setCommandTimes([...commandTimes, 1])}
                                                            disabled={isSubmitting}
                                                        >
                                                            <svg
                                                                xmlns='http://www.w3.org/2000/svg'
                                                                fill='none'
                                                                viewBox='0 0 24 24'
                                                                stroke='currentColor'
                                                                css={tw`h-5 w-5`}
                                                            >
                                                                <path d='M14.613,10c0,0.23-0.188,0.419-0.419,0.419H10.42v3.774c0,0.23-0.189,0.42-0.42,0.42s-0.419-0.189-0.419-0.42v-3.774H5.806c-0.23,0-0.419-0.189-0.419-0.419s0.189-0.419,0.419-0.419h3.775V5.806c0-0.23,0.189-0.419,0.419-0.419s0.42,0.189,0.42,0.419v3.775h3.774C14.425,9.581,14.613,9.77,14.613,10 M17.969,10c0,4.401-3.567,7.969-7.969,7.969c-4.402,0-7.969-3.567-7.969-7.969c0-4.402,3.567-7.969,7.969-7.969C14.401,2.031,17.969,5.598,17.969,10 M17.13,10c0-3.932-3.198-7.13-7.13-7.13S2.87,6.068,2.87,10c0,3.933,3.198,7.13,7.13,7.13S17.13,13.933,17.13,10'></path>
                                                            </svg>
                                                        </Button>
                                                    )}
                                                </div>
                                            </>
                                        ))}
                                    </div>
                                    <p css={tw`mt-1 text-xs text-neutral-200`}>
                                        Specify console commands which should be performed before wiping this server.
                                    </p>
                                </div>
                                <div css={tw`mt-5`}>
                                    <FormikSwitch
                                        name={'force'}
                                        label={'Force Wipe'}
                                        description={
                                            'If enabled this wipe will repeat itself every first Thursday of the month at 8PM CET.'
                                        }
                                        checked={values.force}
                                        readOnly={isSubmitting}
                                    />
                                </div>
                                <div css={tw`mt-5`}>
                                    <FormikSwitch
                                        name={'repeat'}
                                        label={'Repeat Wipe'}
                                        description={
                                            'If enabled this wipe will repeat itself each week, create multiple wipes to wipe your server multiple times a week.'
                                        }
                                        checked={values.repeat}
                                        readOnly={isSubmitting}
                                    />
                                </div>
                            </>
                        )}
                        <div css={tw`mt-5 text-right`}>
                            <Button isLoading={isSubmitting} disabled={isSubmitting} type={'submit'}>
                                {values.schedule ? 'Schedule Wipe' : 'Wipe Now'}
                            </Button>
                        </div>
                    </Form>
                </TitledGreyBox>
            )}
        </Formik>
    );
};
