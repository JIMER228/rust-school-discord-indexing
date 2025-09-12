import createOrUpdateWipe from '@/api/server/wipe/createOrUpdateWipe';
import deleteWipe from '@/api/server/wipe/deleteWipe';
import { Wipe } from '@/api/server/wipe/getWipeData';
import Button from '@/components/elements/Button';
import TitledGreyBox from '@/components/elements/TitledGreyBox';
import { Dialog } from '@/components/elements/dialog';
import { initialValues } from '@/components/server/wipe/CreateWipeBox';
import EditWipeForm from '@/components/server/wipe/EditWipeForm';
import useFlash from '@/plugins/useFlash';
import { ServerContext } from '@/state/server';
import { Form, Formik, FormikHelpers } from 'formik';
import React, { useState } from 'react';
import tw from 'twin.macro';
import { boolean, date, lazy, number, object, string } from 'yup';

interface Props {
    wipes: Wipe[];
    mutate: () => void;
    timezones: string[];
}

export default ({ wipes, mutate, timezones }: Props) => {
    const { clearFlashes, clearAndAddHttpError } = useFlash();

    const uuid = ServerContext.useStoreState((state) => state.server.data!.uuid);
    const timezone = ServerContext.useStoreState((state) => state.server.data!.timezone);

    const [edit, setEdit] = useState<Wipe | null>(null);
    const [commandTimes, setCommandTimes] = useState<number[]>([]);

    const submit = (values: Wipe, { setSubmitting, resetForm }: FormikHelpers<Wipe>) => {
        clearFlashes('server:wipe');

        createOrUpdateWipe(uuid, values, commandTimes, edit!.id)
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
                setEdit(null);
            });
    };

    function getNextForce(): string {
        const now = new Date();

        function getFirstThursday(year: number, month: number): Date {
            const date = new Date(Date.UTC(year, month, 1, 18, 0));
            while (date.getUTCDay() !== 4) {
                date.setUTCDate(date.getUTCDate() + 1);
            }
            return date;
        }

        const thisMonthFirstThursday = getFirstThursday(now.getFullYear(), now.getMonth());

        const targetDate: Date =
            thisMonthFirstThursday > now
                ? thisMonthFirstThursday
                : getFirstThursday(
                      now.getMonth() === 11 ? now.getFullYear() + 1 : now.getFullYear(),
                      (now.getMonth() + 1) % 12
                  );

        const parts = new Intl.DateTimeFormat('en-GB', {
            timeZone: timezone,
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
            hour12: false,
        }).formatToParts(targetDate);

        const timeParts: Record<string, string> = {};
        parts.forEach((part) => {
            timeParts[part.type] = part.value;
        });

        return `${timeParts.year}-${timeParts.month}-${timeParts.day} ${timeParts.hour}:${timeParts.minute}:${timeParts.second}`;
    }

    return (
        <TitledGreyBox title={'Scheduled Wipes'}>
            {wipes.length > 0 ? (
                wipes.map((wipe, index) => (
                    <div key={index} css={[tw`flex items-center justify-center mt-0`, index > 0 && tw`mt-2`]}>
                        <b css={tw`w-2/4`}>{wipe.time ?? getNextForce()}</b>
                        <Button
                            type={'button'}
                            size={'xsmall'}
                            color={'green'}
                            css={tw`mr-2`}
                            onClick={() => setEdit(wipe)}
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
                                    d='m505.99-87.12v3.12h3.12l7.88-7.88-3.12-3.12zm13.76-7.51c.33-.33.33-.85 0-1.18l-1.95-1.95c-.33-.33-.85-.33-1.18 0l-1.63 1.64 3.12 3.12z'
                                    transform='matrix(1.14224 0 0 1.14224-574.96 114.94)'
                                />
                            </svg>
                        </Button>
                        <Button
                            type={'button'}
                            size={'xsmall'}
                            color={'red'}
                            onClick={() => deleteWipe(uuid, wipe.id!).then(() => mutate())}
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
                ))
            ) : (
                <div css={tw`flex justify-center`}>No wipes are scheduled.</div>
            )}
            <Dialog open={!!edit} onClose={() => setEdit(null)} title={'Edit Wipe'}>
                <Formik
                    onSubmit={submit}
                    initialValues={initialValues}
                    validationSchema={object().shape(
                        {
                            name: string().required().max(191),
                            description: string().required(),
                            size: lazy(() =>
                                number()
                                    .nullable()
                                    .when(['level', 'randomLevel'], {
                                        is: (a, b) => a === null && b === false,
                                        then: number().required(),
                                    })
                            ),
                            randomSeed: boolean(),
                            randomLevel: boolean(),
                            seed: lazy(() =>
                                number()
                                    .nullable()
                                    .when(['level', 'randomSeed', 'randomLevel'], {
                                        is: (a, b, c) => a === null && b === false && c === false,
                                        then: number().required(),
                                    })
                            ),
                            level: lazy(() =>
                                string()
                                    .nullable()
                                    .when(['seed', 'randomSeed', 'randomLevel'], {
                                        is: (a, b, c) => a === null && b === false && c === false,
                                        then: string().required(),
                                    })
                            ),
                            files: string().nullable(),
                            blueprints: boolean(),
                            time: date()
                                .nullable()
                                .when('schedule', {
                                    is: true,
                                    then: date()
                                        .required()
                                        .min(new Date().toLocaleString('en', { timeZone: timezone ?? timezones[0] })),
                                }),
                            force: boolean(),
                            repeat: boolean(),
                        },
                        [
                            ['seed', 'randomSeed'],
                            ['level', 'randomSeed'],
                        ]
                    )}
                >
                    {({ submitForm }) => (
                        <Form css={tw`m-0`}>
                            <EditWipeForm wipe={edit!} commandTimes={commandTimes} setCommandTimes={setCommandTimes} />
                            <Dialog.Footer>
                                <Button color='grey' className={'w-full sm:w-auto'} onClick={() => setEdit(null)}>
                                    Cancel
                                </Button>
                                <Button className={'w-full sm:w-auto'} onClick={submitForm}>
                                    Update
                                </Button>
                            </Dialog.Footer>
                        </Form>
                    )}
                </Formik>
            </Dialog>
        </TitledGreyBox>
    );
};
