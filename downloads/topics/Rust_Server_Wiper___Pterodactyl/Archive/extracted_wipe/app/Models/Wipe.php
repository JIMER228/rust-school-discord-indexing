<?php

namespace Pterodactyl\Models;

class Wipe extends Model
{
    public const RESOURCE_NAME = 'wipe';

    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'wipes';

    /**
     * Default values when creating the model. We want to switch to disabling OOM killer
     * on server instances unless the user specifies otherwise in the request.
     *
     * @var array
     */
    protected $attributes = [
        'random_seed' => false,
        'random_level' => false,
        'blueprints' => false,
        'force' => false,
        'repeat' => false,
    ];

    /**
     * The attributes that should be mutated to dates.
     *
     * @var array
     */
    protected $dates = [self::CREATED_AT, self::UPDATED_AT];

    /**
     * Fields that are not mass assignable.
     *
     * @var array
     */
    protected $guarded = ['id', self::CREATED_AT, self::UPDATED_AT];

    /**
     * @var array
     */
    public static array $validationRules = [
        'server_id' => 'bail|required|numeric|exists:servers,id',
        'name' => 'required|string|min:1|max:191',
        'description' => 'required|string',
        'size' => 'nullable|numeric',
        'seed' => 'nullable|numeric',
        'random_seed' => 'boolean',
        'random_level' => 'boolean',
        'level' => 'nullable|url',
        'files' => 'nullable|string',
        'blueprints' => 'boolean',
        'time' => 'nullable|date', // after_or_equal:now can't be used here as it will use the default application timezone instead of the timezone the server has. This will be an issue when submitting older dates using the API request, however, this won't matter as it will still run the wipe the next minute.
        'ran_at' => 'nullable|date', // after_or_equal:now can't be used here as it will use the default application timezone instead of the timezone the server has. This is not an issue, as ran_at can not be set by API.
        'force' => 'boolean',
        'repeat' => 'boolean',
    ];

    /**
     * @return \Illuminate\Database\Eloquent\Relations\BelongsTo
     */
    public function server()
    {
        return $this->belongsTo(Server::class);
    }

    /**
     * @return \Illuminate\Database\Eloquent\Relations\HasMany
     */
    public function commands()
    {
        return $this->hasMany(WipeCommand::class);
    }
}
