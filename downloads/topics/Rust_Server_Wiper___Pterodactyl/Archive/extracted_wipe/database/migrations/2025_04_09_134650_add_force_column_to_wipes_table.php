<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * Run the migrations.
     */
    public function up(): void
    {
        Schema::table('wipes', function (Blueprint $table) {
            $table->boolean('force')->after('blueprints');
            $table->datetime('time')->after('force')->nullable()->change();
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::table('wipes', function (Blueprint $table) {
            $table->dropColumn('force');
            $table->datetime('time')->after('blueprints')->change();
        });
    }
};
